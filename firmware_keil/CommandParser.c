#include "CommandParser.h"
#include "ConfigMgr.h"
#include "StorageMgr.h"
#include "FeatTag.h"
#include <string.h>

/* Command parser layer. Full implementation per CMD.md.
 * Target MCU: 8051 (AT89C55WD, IROM 0x5000 = 20KB).
 *
 * Dual EEPROM layout (see StorageMgr.h):
 *   chip0 0x50 metadata:
 *     FIRST_BLOCK_CFG_OFF  0x0000 config zone (256B)
 *     FIRST_BLOCK_BLOCKTAB 0x0100 block table (block_entry_t fixed array)
 *     FIRST_BLOCK_KVIDX    0x1000 kv index zone (kv_entry_t serialized)
 *     FIRST_BLOCK_BITMAP   0x1C00 allocation bitmap (reserved)
 *   chip1 0x51 data:
 *     0x0000 data head (16B), then KV_RECORD appended sequentially.
 */

#define FIRMWARE_VERSION   "V1.0.0"
#define PROTOCOL_VERSION   "P1.0"
#define STORAGE_FMT_VER    "S1.0"

#define SESSION_TIMEOUT_TICKS  300u
#define DATA_BODY_START    DATA_HEAD_SIZE
#define BLOCK_TAB_MAX      256u
#define KVIDX_MAX          256u
#define BLK_REC_SIZE   ((uint16_t)sizeof(block_entry_t))
#define KVIDX_REC_SIZE 13u

static uint16_t s_storage_size = 0;
static uint8_t  s_task      = 0;
static uint8_t  s_authed    = 0;
static uint16_t s_auth_tick = 0;

static uint16_t CMD_ReadU16(uint16_t addr) {
  return (uint16_t)EEPROM_ReadByte(addr) |
         ((uint16_t)EEPROM_ReadByte(addr + 1) << 8);
}

static void CMD_WriteU16(uint16_t addr, uint16_t val) {
  EEPROM_WriteByte(addr, (uint8_t)(val & 0xFF));
  EEPROM_WriteByte(addr + 1, (uint8_t)(val >> 8));
}

static void CMD_Reply(const char *tag, uint8_t val) {
  Uart1_SendString((char *)tag);
  Uart1_SendString("+");
  Uart1_SendNumber(val);
  Uart1_SendString("\r\n");
}

static int CMD_Compare(const char *a, const char *b) {
  if (a == 0 || b == 0) return 0;
  return strcmp(a, b) == 0;
}

static char *CMD_StripQuote(char *s) {
  uint8_t len;
  if (s == 0) return s;
  len = (uint8_t)strlen(s);
  if (len >= 2 && s[0] == '"' && s[len - 1] == '"') {
    s[len - 1] = '\0';
    return s + 1;
  }
  if (len >= 1 && s[0] == '"') return s + 1;
  return s;
}

static uint16_t CMD_AtoU16(char *str) {
  uint32_t val = 0;
  if (str == 0) return 0;
  while (*str >= '0' && *str <= '9') {
    val = val * 10u + (uint32_t)(*str - '0');
    if (val > 0xFFFFu) { val = 0xFFFFu; break; }
    str++;
  }
  return (uint16_t)val;
}

static uint8_t CMD_Atoi(char *str) {
  uint16_t v = CMD_AtoU16(str);
  return (uint8_t)(v > 0xFFu ? 0xFFu : v);
}

static uint8_t CMD_HexNibble(char c) {
  if (c >= '0' && c <= '9') return (uint8_t)(c - '0');
  if (c >= 'A' && c <= 'F') return (uint8_t)(c - 'A' + 10);
  if (c >= 'a' && c <= 'f') return (uint8_t)(c - 'a' + 10);
  return 0xFF;
}

static uint8_t CMD_HexToBytes(char *hex, uint8_t *buf, uint8_t max) {
  uint8_t n = 0, hi, lo;
  if (hex == 0) return 0;
  while (hex[0] && hex[1] && n < max) {
    hi = CMD_HexNibble(hex[0]);
    lo = CMD_HexNibble(hex[1]);
    if (hi == 0xFF || lo == 0xFF) break;
    buf[n++] = (uint8_t)((hi << 4) | lo);
    hex += 2;
  }
  return n;
}

static int CMD_Split(char *str, char delim, char **tokens, int max_tokens) {
  int count = 0;
  if (str == 0) return 0;
  while (count < max_tokens) {
    tokens[count++] = str;
    while (*str != '\0' && *str != delim) str++;
    if (*str == '\0') break;
    *str++ = '\0';
  }
  return count;
}

static uint8_t CMD_CheckAuth(void) {
  cfg_t cfg;
  ConfigMgr_Load(&cfg);
  if (!(cfg.flags & FLAG_INITIALIZED)) { CMD_Reply("ERR", ERR_NOT_AUTH); return 0; }
  if ((cfg.flags & FLAG_PWD_AUTH) && !s_authed) {
    uint8_t i, allzero = 1;
    for (i = 0; i < 16; i++) { if (cfg.pwd_hash[i] != 0) { allzero = 0; break; } }
    if (!allzero) { CMD_Reply("ERR", ERR_NOT_AUTH); return 0; }
    s_authed = 1;
  }
  return 1;
}

static uint8_t CMD_CheckWriteProtect(void) {
  cfg_t cfg;
  ConfigMgr_Load(&cfg);
  if (cfg.flags & FLAG_READ_ONLY) { CMD_Reply("ERR", ERR_WRITE_PROTECT); return 0; }
  return 1;
}

/* 密码比对：返回 1 匹配、0 不匹配、2 未设置密码。
 * 复用调用方已有的 cfg 缓存，不额外占用持久变量。 */
static uint8_t CMD_PwdMatches(cfg_t *cfg, char *pass) {
  uint8_t hash[16];
  uint8_t i, match = 1, allzero = 1;
  for (i = 0; i < 16; i++) { if (cfg->pwd_hash[i] != 0) { allzero = 0; break; } }
  if (allzero) return 2;
  FeatTag_Checksum(pass, (uint8_t)strlen(pass), hash);
  for (i = 0; i < 16; i++) { if (cfg->pwd_hash[i] != hash[i]) { match = 0; break; } }
  return match;
}

/* 命令统一前置：置任务态 + 鉴权 (+ 只读锁)。
 * need_write=1 时同时检查写保护。返回 0 表示已被拒绝（已回 ERR）。 */
static uint8_t CMD_Begin(uint8_t task, uint8_t need_write) {
  s_task = task;
  if (!CMD_CheckAuth()) { s_task = TASK_IDLE; return 0; }
  if (need_write && !CMD_CheckWriteProtect()) { s_task = TASK_IDLE; return 0; }
  return 1;
}

uint8_t CMD_IsAuthorized(void) { return s_authed; }

void CMD_SessionTick(void) {
  if (s_authed && s_auth_tick > 0) { if (--s_auth_tick == 0) s_authed = 0; }
}

void CMD_PARSER_SET_STORAGE(uint16_t addr) { s_storage_size = addr; }

/* ---- Block table management ---- */

static uint8_t CMD_ReadBlockSlot(uint16_t idx, block_entry_t *out) {
  uint16_t off = (uint16_t)(FIRST_BLOCK_BLOCKTAB + idx * BLK_REC_SIZE);
  uint8_t  j;
  if (idx >= BLOCK_TAB_MAX) return 0;
  out->block_id = CMD_ReadU16(off);
  if (out->block_id == 0xFFFF) return 0;
  for (j = 0; j < BLOCK_NAME_LEN; j++)
    out->name[j] = EEPROM_ReadByte((uint16_t)(off + 2 + j));
  out->flags        = EEPROM_ReadByte((uint16_t)(off + 2 + BLOCK_NAME_LEN));
  out->kv_count     = CMD_ReadU16((uint16_t)(off + 3 + BLOCK_NAME_LEN));
  out->kv_index_off = CMD_ReadU16((uint16_t)(off + 5 + BLOCK_NAME_LEN));
  out->kv_index_len = CMD_ReadU16((uint16_t)(off + 7 + BLOCK_NAME_LEN));
  out->data_start   = 0;
  out->data_used    = 0;
  return 1;
}

static void CMD_WriteBlockSlot(uint16_t idx, const block_entry_t *blk) {
  uint16_t off = (uint16_t)(FIRST_BLOCK_BLOCKTAB + idx * BLK_REC_SIZE);
  uint8_t  j;
  CMD_WriteU16(off, blk->block_id);
  for (j = 0; j < BLOCK_NAME_LEN; j++)
    EEPROM_WriteByte((uint16_t)(off + 2 + j), blk->name[j]);
  EEPROM_WriteByte((uint16_t)(off + 2 + BLOCK_NAME_LEN), blk->flags);
  CMD_WriteU16((uint16_t)(off + 3 + BLOCK_NAME_LEN), blk->kv_count);
  CMD_WriteU16((uint16_t)(off + 5 + BLOCK_NAME_LEN), blk->kv_index_off);
  CMD_WriteU16((uint16_t)(off + 7 + BLOCK_NAME_LEN), blk->kv_index_len);
}

static void CMD_ClearBlockSlot(uint16_t idx) {
  uint16_t off = (uint16_t)(FIRST_BLOCK_BLOCKTAB + idx * BLK_REC_SIZE);
  uint8_t  j;
  CMD_WriteU16(off, 0xFFFF);
  for (j = 0; j < BLOCK_NAME_LEN; j++) EEPROM_WriteByte((uint16_t)(off + 2 + j), 0x00);
  EEPROM_WriteByte((uint16_t)(off + 2 + BLOCK_NAME_LEN), 0x00);
}

static uint16_t CMD_FindFreeBlockSlot(void) {
  uint16_t i;
  for (i = 0; i < BLOCK_TAB_MAX; i++) {
    if (CMD_ReadU16((uint16_t)(FIRST_BLOCK_BLOCKTAB + i * BLK_REC_SIZE)) == 0xFFFF) return i;
  }
  return 0xFFFF;
}

static uint16_t CMD_NextBlockId(void) {
  uint16_t i, max_id = 0;
  for (i = 0; i < BLOCK_TAB_MAX; i++) {
    block_entry_t b;
    if (!CMD_ReadBlockSlot(i, &b)) continue;
    if (b.block_id > max_id) max_id = b.block_id;
  }
  return (uint16_t)(max_id + 1);
}

static uint16_t CMD_LoadBlockById(uint16_t want_id, block_entry_t *out) {
  uint16_t i;
  for (i = 0; i < BLOCK_TAB_MAX; i++) {
    if (!CMD_ReadBlockSlot(i, out)) continue;
    if (out->block_id == want_id) return i;
  }
  return 0xFFFF;
}

static uint16_t CMD_LoadBlockByName(char *name, block_entry_t *out) {
  uint16_t i;
  uint8_t  j;
  for (i = 0; i < BLOCK_TAB_MAX; i++) {
    if (!CMD_ReadBlockSlot(i, out)) continue;
    for (j = 0; j < BLOCK_NAME_LEN; j++) {
      if (name[j] == '\0') break;
      if (out->name[j] != (uint8_t)name[j]) break;
    }
    if (name[j] == '\0') return i;
  }
  return 0xFFFF;
}

static uint8_t CMD_BlockNameExists(char *name) {
  block_entry_t b;
  return (CMD_LoadBlockByName(name, &b) != 0xFFFF) ? 1 : 0;
}

static void CMD_SetBlockName(block_entry_t *blk, char *name) {
  uint8_t j;
  for (j = 0; j < BLOCK_NAME_LEN; j++) blk->name[j] = 0;
  if (name == 0) return;
  for (j = 0; j < BLOCK_NAME_LEN && name[j] != '\0'; j++) blk->name[j] = (uint8_t)name[j];
}

static void CMD_GetBlockName(const block_entry_t *blk, char *out) {
  uint8_t j;
  for (j = 0; j < BLOCK_NAME_LEN; j++) out[j] = (char)blk->name[j];
  out[BLOCK_NAME_LEN] = '\0';
}

/* ---- KV index management ---- */

static uint16_t CMD_KvEntryOff(uint16_t idx) {
  return (uint16_t)(FIRST_BLOCK_KVIDX + idx * KVIDX_REC_SIZE);
}

static void CMD_ReadKvEntry(uint16_t idx, kv_entry_t *e) {
  uint16_t off = CMD_KvEntryOff(idx);
  e->block_id  = CMD_ReadU16((uint16_t)(off + 0));
  e->key_len   = CMD_ReadU16((uint16_t)(off + 2));
  e->key_off   = CMD_ReadU16((uint16_t)(off + 4));
  e->slave     = EEPROM_ReadByte((uint16_t)(off + 6));
  e->data_addr = CMD_ReadU16((uint16_t)(off + 7));
  e->data_len  = CMD_ReadU16((uint16_t)(off + 9));
  e->enc_algo  = EEPROM_ReadByte((uint16_t)(off + 11));
  e->flags     = EEPROM_ReadByte((uint16_t)(off + 12));
}

static void CMD_WriteKvEntry(uint16_t idx, const kv_entry_t *e) {
  uint16_t off = CMD_KvEntryOff(idx);
  CMD_WriteU16((uint16_t)(off + 0), e->block_id);
  CMD_WriteU16((uint16_t)(off + 2), e->key_len);
  CMD_WriteU16((uint16_t)(off + 4), e->key_off);
  EEPROM_WriteByte((uint16_t)(off + 6), e->slave);
  CMD_WriteU16((uint16_t)(off + 7), e->data_addr);
  CMD_WriteU16((uint16_t)(off + 9), e->data_len);
  EEPROM_WriteByte((uint16_t)(off + 11), e->enc_algo);
  EEPROM_WriteByte((uint16_t)(off + 12), e->flags);
}

static uint16_t CMD_FindKeyIdx(uint16_t block_id, const char *key) {
  uint16_t klen = (uint16_t)strlen(key);
  uint16_t i;
  for (i = 0; i < KVIDX_MAX; i++) {
    kv_entry_t e;
    uint16_t k;
    CMD_ReadKvEntry(i, &e);
    if (e.block_id != block_id) continue;
    if (!(e.flags & KV_FLAG_VALID)) continue;
    if (e.key_len != klen) continue;
    for (k = 0; k < klen; k++) {
      if (EEPROM_ReadByte((uint16_t)(e.key_off + k)) != (uint8_t)key[k]) break;
    }
    if (k == klen) return i;
  }
  return 0xFFFF;
}

static uint16_t CMD_AllocKvSlot(void) {
  uint16_t i;
  for (i = 0; i < KVIDX_MAX; i++) {
    if (CMD_ReadU16(CMD_KvEntryOff(i)) == 0xFFFF) return i;
  }
  return 0xFFFF;
}

static uint16_t CMD_KeyPoolAlloc(uint16_t len) {
  uint16_t i, end = (uint16_t)(FIRST_BLOCK_KVIDX + KVIDX_MAX * KVIDX_REC_SIZE);
  for (i = 0; i < KVIDX_MAX; i++) {
    kv_entry_t e;
    CMD_ReadKvEntry(i, &e);
    if (e.block_id == 0xFFFF) continue;
    if ((uint16_t)(e.key_off + e.key_len) > end) end = (uint16_t)(e.key_off + e.key_len);
  }
  if ((uint32_t)end + len > FIRST_BLOCK_BITMAP) return 0xFFFF;
  return end;
}

/* ---- Data plane KV_RECORD serialization ---- */

static uint16_t CMD_KvRecLen(uint16_t key_len, uint16_t val_len) {
  return (uint16_t)(KV_REC_HDR_LEN + key_len + val_len + 2 + 1 + 1);
}

static uint16_t CMD_AppendKvRecord(uint16_t block_id, const char *key,
                                   const uint8_t *value, uint16_t val_len,
                                   uint8_t enc_algo, uint8_t flags) {
  uint16_t key_len = (uint16_t)strlen(key);
  uint16_t total   = CMD_KvRecLen(key_len, val_len);
  uint16_t addr    = CMD_ReadU16(CFG_ADDR_DATA_NEXT);
  uint16_t total_cap = CMD_ReadU16(CFG_ADDR_DATA_TOTAL);
  uint16_t i;

  if (total_cap < DATA_BODY_START) return 0xFFFF;
  if (addr < DATA_BODY_START) return 0xFFFF;
  if ((uint32_t)addr + total > total_cap) return 0xFFFF;

  EEPROM_SetAddress(IC_1_24CXX);
  EEPROM_WriteByte((uint16_t)(addr + 0x00), KV_REC_MAGIC);
  EEPROM_WriteByte((uint16_t)(addr + 0x01), KV_REC_STATE_VALID);
  CMD_WriteU16((uint16_t)(addr + KV_OFF_BLOCK_ID), block_id);
  CMD_WriteU16((uint16_t)(addr + KV_OFF_KEY_LEN), key_len);
  CMD_WriteU16((uint16_t)(addr + KV_OFF_VAL_LEN), val_len);
  EEPROM_WriteByte((uint16_t)(addr + KV_OFF_ENC_ALGO), enc_algo);
  EEPROM_WriteByte((uint16_t)(addr + KV_OFF_FLAGS), flags);
  EEPROM_WriteByte((uint16_t)(addr + KV_OFF_IV_LEN), 0);
  for (i = 0; i < 16; i++) EEPROM_WriteByte((uint16_t)(addr + KV_OFF_IV + i), 0x00);
  EEPROM_WriteByte((uint16_t)(addr + KV_OFF_TAG_LEN), 0);
  for (i = 0; i < 16; i++) EEPROM_WriteByte((uint16_t)(addr + KV_OFF_TAG + i), 0x00);
  for (i = 0; i < key_len; i++)
    EEPROM_WriteByte((uint16_t)(addr + KV_OFF_KEY + i), (uint8_t)key[i]);
  for (i = 0; i < val_len; i++)
    EEPROM_WriteByte((uint16_t)(addr + KV_OFF_KEY + key_len + i), value[i]);
  {
    uint16_t crc_addr = (uint16_t)(addr + KV_OFF_KEY + key_len + val_len);
    uint16_t crc = 0;
    for (i = 0; i < val_len; i++) crc = (uint16_t)(crc + (uint16_t)value[i] * (i + 1));
    CMD_WriteU16(crc_addr, crc);
    EEPROM_WriteByte((uint16_t)(crc_addr + 2), KV_REC_VER);
    EEPROM_WriteByte((uint16_t)(crc_addr + 3), KV_REC_ETX);
  }
  addr = (uint16_t)(addr + total);
  CMD_WriteU16(CFG_ADDR_DATA_NEXT, addr);
  {
    uint16_t used = CMD_ReadU16(CFG_ADDR_DATA_USED);
    uint16_t hwm  = CMD_ReadU16(CFG_ADDR_DATA_HWM);
    used = (uint16_t)(used + total);
    if (addr > hwm) hwm = addr;
    CMD_WriteU16(CFG_ADDR_DATA_USED, used);
    CMD_WriteU16(CFG_ADDR_DATA_HWM, hwm);
    if (total_cap > 0) {
      CMD_WriteU16(CFG_ADDR_DATA_FREE, (uint16_t)(total_cap - used));
      EEPROM_WriteByte(CFG_ADDR_DATA_USAGE, (uint8_t)(((uint32_t)used * 100u) / total_cap));
    }
  }
  return (uint16_t)(addr - total);
}

/* 读记录头部长度字段：val_len 经 *val_len 返回，key_len 作返回值。
 * 记录地址非法或魔数不符时返回 0xFFFF。集中处理、避免各站重复算偏移。 */
static uint16_t CMD_ReadKvRecordLens(uint16_t data_addr, uint16_t *val_len) {
  if (data_addr < DATA_BODY_START) return 0xFFFF;
  EEPROM_SetAddress(IC_1_24CXX);
  if (EEPROM_ReadByte(data_addr) != KV_REC_MAGIC) return 0xFFFF;
  *val_len = CMD_ReadU16((uint16_t)(data_addr + KV_OFF_VAL_LEN));
  return CMD_ReadU16((uint16_t)(data_addr + KV_OFF_KEY_LEN));
}

static void CMD_DeleteKvRecord(uint16_t data_addr) {
  if (data_addr < DATA_BODY_START) return;
  EEPROM_SetAddress(IC_1_24CXX);
  EEPROM_WriteByte((uint16_t)(data_addr + 0x01), KV_REC_STATE_DEL);
}

/* 通用槽位释放：删数据记录(墓碑) + 清索引项(block_id 置 0xFFFF)。
 * 复用于 DELETE KEY / 块清理 / 块格式化，避免三处各写一遍。 */
static void CMD_FreeKvSlot(uint16_t idx) {
  kv_entry_t e;
  CMD_ReadKvEntry(idx, &e);
  if (e.block_id == 0xFFFF) return;
  if (e.flags & KV_FLAG_VALID) CMD_DeleteKvRecord(e.data_addr);
  EEPROM_SetAddress(IC_0_24C64);
  CMD_WriteU16(CMD_KvEntryOff(idx), 0xFFFF);
}

static uint8_t CMD_UpdateKvRecordValue(uint16_t data_addr, const uint8_t *value, uint16_t val_len) {
  uint16_t key_len, i;
  if (data_addr < DATA_BODY_START) return 0;
  EEPROM_SetAddress(IC_1_24CXX);
  if (EEPROM_ReadByte(data_addr) != KV_REC_MAGIC) return 0;
  key_len = CMD_ReadU16((uint16_t)(data_addr + KV_OFF_KEY_LEN));
  for (i = 0; i < val_len; i++)
    EEPROM_WriteByte((uint16_t)(data_addr + KV_OFF_KEY + key_len + i), value[i]);
  return 1;
}

/* 统一入口：block_flag=="0" 时按名字定位，否则按数字 id 定位。
 * 找到返回槽位索引(0..BLOCK_TAB_MAX-1)，未找到返回 0xFFFF。 */
static uint16_t CMD_ResolveBlock(char *block_flag, char *block_identifier,
                                 block_entry_t *out) {
  if (CMD_Compare(block_flag, "0"))
    return CMD_LoadBlockByName(block_identifier, out);
  return CMD_LoadBlockById(CMD_AtoU16(block_identifier), out);
}

/* 统一的 KV 写入：idx==0xFFFF 表示新建（分配槽位/键池，kv_count+1），
 * 否则更新既有槽位（等长原地覆写，否则删旧记录+追新记录+回写索引）。
 * 成功返回 ERR_OK；已回带 ERR 应答。 */
static uint8_t CMD_PutKeyValue(block_entry_t *blk, uint16_t blk_slot,
                               uint16_t idx, const char *key,
                               const uint8_t *value, uint16_t val_len) {
  kv_entry_t e;
  uint16_t key_len = (uint16_t)strlen(key);
  uint16_t kv_slot = idx, key_off, data_addr;

  if (key_len == 0) { CMD_Reply("ERR", ERR_PARAM); return ERR_PARAM; }

  if (idx == 0xFFFF) {
    /* 新建：分配索引槽 + 键池空间 */
    kv_slot = CMD_AllocKvSlot();
    if (kv_slot == 0xFFFF) { CMD_Reply("ERR", ERR_BITMAP_FULL); return ERR_BITMAP_FULL; }
    EEPROM_SetAddress(IC_0_24C64);
    key_off = CMD_KeyPoolAlloc(key_len);
    if (key_off == 0xFFFF) { CMD_Reply("ERR", ERR_DATA_FULL); return ERR_DATA_FULL; }
    { uint16_t i; for (i = 0; i < key_len; i++)
        EEPROM_WriteByte((uint16_t)(key_off + i), (uint8_t)key[i]); }
    e.enc_algo = ENC_ALGO_NONE;
    e.flags    = KV_FLAG_VALID;
  } else {
    /* 更新：读旧索引项，等长就地覆写，否则删旧+追新 */
    CMD_ReadKvEntry(idx, &e);
    if (e.data_len == val_len && CMD_UpdateKvRecordValue(e.data_addr, value, val_len)) {
      return ERR_OK;
    }
    key_off = e.key_off;
    if (e.flags & KV_FLAG_VALID) CMD_DeleteKvRecord(e.data_addr);
  }

  data_addr = CMD_AppendKvRecord(blk->block_id, key, value, val_len,
                                 e.enc_algo, KV_FLAG_VALID);
  if (data_addr == 0xFFFF) { CMD_Reply("ERR", ERR_DATA_FULL); return ERR_DATA_FULL; }

  e.block_id  = blk->block_id;
  e.key_len   = key_len;
  e.key_off   = key_off;
  e.slave     = IC_1_24CXX;
  e.data_addr = data_addr;
  e.data_len  = val_len;
  EEPROM_SetAddress(IC_0_24C64);
  CMD_WriteKvEntry(kv_slot, &e);
  if (idx == 0xFFFF) {
    blk->kv_count = (uint16_t)(blk->kv_count + 1);
    CMD_WriteBlockSlot(blk_slot, blk);
  }
  return ERR_OK;
}

static uint8_t CMD_DoCreateBlock(char *name) {
  block_entry_t blk;
  uint16_t slot;
  if (name == 0 || strlen(name) == 0) { CMD_Reply("ERR", ERR_PARAM); return 0; }
  if (CMD_BlockNameExists(name)) { CMD_Reply("ERR", ERR_BLOCK_EXIST); return 0; }
  slot = CMD_FindFreeBlockSlot();
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BITMAP_FULL); return 0; }
  blk.block_id     = CMD_NextBlockId();
  CMD_SetBlockName(&blk, name);
  blk.flags        = 0;
  blk.kv_count     = 0;
  blk.kv_index_off = FIRST_BLOCK_KVIDX;
  blk.kv_index_len = 0;
  CMD_WriteBlockSlot(slot, &blk);
  Uart1_SendString("DATA+");
  Uart1_SendNumber(blk.block_id);
  Uart1_SendString("\r\n");
  return 1;
}

static uint8_t CMD_DoCreateKey(char *block_flag, char *block_identifier,
                               char *key, char *value) {
  block_entry_t blk;
  uint16_t slot;
  uint16_t val_len = (uint16_t)strlen(value);

  slot = CMD_ResolveBlock(block_flag, block_identifier, &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); return 0; }
  if (CMD_FindKeyIdx(blk.block_id, key) != 0xFFFF) { CMD_Reply("ERR", ERR_KEY_EXIST); return 0; }
  return (CMD_PutKeyValue(&blk, slot, 0xFFFF, key, (const uint8_t *)value, val_len) == ERR_OK);
}

static void CMD_EmitBlockKv(uint16_t block_id) {
  uint16_t i, count = 0;
  for (i = 0; i < KVIDX_MAX; i++) {
    kv_entry_t e;
    uint16_t k, val_len, key_len;
    CMD_ReadKvEntry(i, &e);
    if (e.block_id != block_id) continue;
    if (!(e.flags & KV_FLAG_VALID)) continue;
    if (count > 0) Uart1_SendString(",");
    for (k = 0; k < e.key_len; k++)
      Uart1_SendByte(EEPROM_ReadByte((uint16_t)(e.key_off + k)));
    Uart1_SendString("=");
    key_len = CMD_ReadKvRecordLens(e.data_addr, &val_len);
    if (key_len == 0xFFFF) { count++; continue; }
    for (k = 0; k < val_len; k++)
      Uart1_SendByte(EEPROM_ReadByte((uint16_t)(e.data_addr + KV_OFF_KEY + key_len + k)));
    count++;
  }
}

static void CMD_EmitBlockEntry(block_entry_t *blk) {
  char name[BLOCK_NAME_LEN + 1];
  CMD_GetBlockName(blk, name);
  Uart1_SendString("[");
  Uart1_SendString(name);
  Uart1_SendString(";");
  Uart1_SendNumber(blk->block_id);
  Uart1_SendString("](");
  CMD_EmitBlockKv(blk->block_id);
  Uart1_SendString(")");
}

static void CMD_PurgeBlockKv(uint16_t block_id) {
  uint16_t i;
  for (i = 0; i < KVIDX_MAX; i++) {
    kv_entry_t e;
    CMD_ReadKvEntry(i, &e);
    if (e.block_id != block_id) continue;
    CMD_FreeKvSlot(i);
  }
}

static void CMD_EmitAllBlocks(void) {
  block_entry_t blk;
  uint16_t i, n = 0;
  Uart1_SendString("DATA+");
  for (i = 0; i < BLOCK_TAB_MAX; i++) {
    if (!CMD_ReadBlockSlot(i, &blk)) continue;
    if (n > 0) Uart1_SendString("|");
    CMD_EmitBlockEntry(&blk);
    n++;
  }
  Uart1_SendString("\r\n");
}

/* 删除一个已定位的块：清空其 KV 并释放块表槽位。block_flag=="0" 用名字定位。 */
static void CMD_DoDeleteBlock(char *block_flag, char *block_identifier, uint8_t by_id) {
  block_entry_t blk;
  uint16_t slot;
  if (by_id)
    slot = CMD_LoadBlockById(CMD_AtoU16(block_identifier), &blk);
  else
    slot = CMD_ResolveBlock(block_flag, block_identifier, &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); return; }
  CMD_PurgeBlockKv(blk.block_id);
  EEPROM_SetAddress(IC_0_24C64);
  CMD_ClearBlockSlot(slot);
  CMD_Reply("RESULT", ERR_OK);
}

/* ---- Start commands ---- */

void CMD_INIT(char *device_name, char *password,
              uint8_t encrypt_type, uint8_t data_size_code) {
  cfg_t cfg;
  if (device_name == 0 || strlen(device_name) == 0) { CMD_Reply("ERR", ERR_PARAM); return; }
  if (data_size_code > DATA_SIZE_MAX) { CMD_Reply("ERR", ERR_SIZE_CODE_INVALID); return; }
  if (data_size_code == 0) data_size_code = DATA_SIZE_24C64;

  EEPROM_SetAddress(IC_0_24C64);
  EEPROM_Fill(0, DEV_EEPROM_SIZE, 0x00);

  ConfigMgr_Defaults(&cfg);
  ConfigMgr_SetName(&cfg, device_name);
  ConfigMgr_SetPassword(&cfg, password);
  cfg.encrypt = encrypt_type;
  cfg.flags |= FLAG_INITIALIZED;
  ConfigMgr_Save(&cfg);

  StorageMgr_InitDevice(device_name, password, data_size_code);

  s_authed = 1;
  s_auth_tick = SESSION_TIMEOUT_TICKS;
  CMD_Reply("RESULT", ERR_OK);
}

void CMD_ECHO(void) { CMD_Reply("RESULT", ERR_OK); }

void CMD_INFO(void) {
  uint8_t i;
  uint8_t flags, enc_type, block_cnt = 0;
  uint16_t key_cnt = 0;
  char name[CFG_NAME_LEN + 1];

  flags = ConfigMgr_GetFlags();
  if (!(flags & FLAG_INITIALIZED)) {
    Uart1_SendString("INIT=0+");
    Uart1_SendString(FIRMWARE_VERSION);
    Uart1_SendString("\r\n");
    return;
  }
  for (i = 0; i < CFG_NAME_LEN; i++) name[i] = (char)EEPROM_ReadByte(CFG_ADDR_NAME + i);
  name[CFG_NAME_LEN] = '\0';
  {
    block_entry_t blk;
    uint16_t b, k;
    for (b = 0; b < BLOCK_TAB_MAX; b++) {
      if (!CMD_ReadBlockSlot(b, &blk)) continue;
      block_cnt++;
      for (k = 0; k < KVIDX_MAX; k++) {
        kv_entry_t e;
        CMD_ReadKvEntry(k, &e);
        if (e.block_id == blk.block_id && (e.flags & KV_FLAG_VALID)) key_cnt++;
      }
    }
  }
  enc_type = EEPROM_ReadByte(CFG_ADDR_ENC_ALGO);
  Uart1_SendString("INFO+");
  Uart1_SendString(name);
  Uart1_SendString("+");
  Uart1_SendString(FIRMWARE_VERSION);
  Uart1_SendString("+");
  Uart1_SendString((flags & FLAG_PWD_AUTH) ? "ENABLED" : "DISABLED");
  Uart1_SendString("+");
  Uart1_SendNumber(ConfigMgr_GetAccessCount());
  Uart1_SendString("+");
  Uart1_SendNumber(block_cnt);
  Uart1_SendString("+");
  Uart1_SendNumber(key_cnt);
  Uart1_SendString("+");
  switch (enc_type) {
    case ENC_ALGO_AES128_CBC: Uart1_SendString("AES");   break;
    case ENC_ALGO_AES128_GCM: Uart1_SendString("AES");   break;
    case ENC_ALGO_XOR:        Uart1_SendString("XOR");   break;
    case ENC_ALGO_CAESAR:     Uart1_SendString("CESAR"); break;
    case ENC_ALGO_RC4:        Uart1_SendString("RC4");   break;
    default:                  Uart1_SendString("NON");   break;
  }
  Uart1_SendString("+");
  Uart1_SendNumber((uint16_t)((uint16_t)EEPROM_ReadByte(CFG_ADDR_DEV_SIZE) * 2048));
  Uart1_SendString("\r\n");
}

void CMD_STATUS(void) {
  Uart1_SendString("STATUS+");
  Uart1_SendNumber(s_task);
  Uart1_SendString("\r\n");
}

void CMD_USAGE(void) {
  Uart1_SendString("USAGE+");
  Uart1_SendNumber(CMD_ReadU16(CFG_ADDR_DATA_TOTAL));
  Uart1_SendString("+");
  Uart1_SendNumber(CMD_ReadU16(CFG_ADDR_DATA_USED));
  Uart1_SendString("+");
  Uart1_SendNumber(CMD_ReadU16(CFG_ADDR_DATA_FREE));
  Uart1_SendString("+");
  Uart1_SendNumber(CMD_ReadU16(CFG_ADDR_DATA_HWM));
  Uart1_SendString("+");
  Uart1_SendNumber(CMD_ReadU16(CFG_ADDR_DATA_NEXT));
  Uart1_SendString("+");
  Uart1_SendNumber(EEPROM_ReadByte(CFG_ADDR_DATA_USAGE));
  Uart1_SendString("\r\n");
}

void CMD_VERSION(void) {
  Uart1_SendString("VERSION+");
  Uart1_SendString(FIRMWARE_VERSION);
  Uart1_SendString("+");
  Uart1_SendString(PROTOCOL_VERSION);
  Uart1_SendString("+");
  Uart1_SendString(STORAGE_FMT_VER);
  Uart1_SendString("\r\n");
}

/* ---- AUTH ---- */

void CMD_AUTH_CREATE(char *password) {
  cfg_t cfg;
  ConfigMgr_Load(&cfg);
  ConfigMgr_SetPassword(&cfg, password);
  cfg.flags |= (FLAG_PWD_AUTH | FLAG_INITIALIZED);
  ConfigMgr_Save(&cfg);
  s_authed = 1;
  s_auth_tick = SESSION_TIMEOUT_TICKS;
  Uart1_SendString("AUTH+0\r\n");
}

void CMD_AUTH_VERIFY(char *password) {
  cfg_t cfg;
  uint8_t r;
  ConfigMgr_Load(&cfg);
  r = CMD_PwdMatches(&cfg, password);
  if (r == 2) { Uart1_SendString("AUTH+2\r\n"); s_authed = 1; return; }
  if (r) {
    s_authed = 1;
    s_auth_tick = SESSION_TIMEOUT_TICKS;
    Uart1_SendString("AUTH+0\r\n");
  } else {
    s_authed = 0;
    Uart1_SendString("AUTH+1\r\n");
  }
}

void CMD_AUTH_ENABLE(void) {
  cfg_t cfg;
  ConfigMgr_Load(&cfg);
  cfg.flags |= FLAG_PWD_AUTH;
  ConfigMgr_Save(&cfg);
  Uart1_SendString("AUTH+0\r\n");
}

void CMD_AUTH_DISABLE(void) {
  cfg_t cfg;
  ConfigMgr_Load(&cfg);
  cfg.flags &= (uint8_t)~FLAG_PWD_AUTH;
  ConfigMgr_Save(&cfg);
  s_authed = 1;
  Uart1_SendString("AUTH+0\r\n");
}

void CMD_AUTH_VERIFYOUT(void) {
  s_authed = 0;
  s_auth_tick = 0;
  Uart1_SendString("AUTH+0\r\n");
}

void CMD_AUTH_CHANGE(char *old_pass, char *new_pass) {
  cfg_t cfg;
  ConfigMgr_Load(&cfg);
  /* 已设密码则必须校验旧密码；未设密码直接改 */
  if (CMD_PwdMatches(&cfg, old_pass) == 0) { Uart1_SendString("AUTH+1\r\n"); return; }
  ConfigMgr_SetPassword(&cfg, new_pass);
  cfg.flags |= FLAG_PWD_AUTH;
  ConfigMgr_Save(&cfg);
  Uart1_SendString("AUTH+0\r\n");
}

void CMD_AUTH_RESET(char *recovery_key) {
  if (CMD_Compare(recovery_key, "ARCHIVUM")) {
    cfg_t cfg;
    uint8_t i;
    ConfigMgr_Load(&cfg);
    cfg.flags &= (uint8_t)~FLAG_PWD_AUTH;
    for (i = 0; i < 16; i++) cfg.pwd_hash[i] = 0;
    ConfigMgr_Save(&cfg);
    Uart1_SendString("AUTH+0\r\n");
  } else {
    Uart1_SendString("AUTH+1\r\n");
  }
}

/* ---- READ ---- */

void CMD_READ(char *unit) {
  s_task = TASK_READ;
  if (CMD_Compare(unit, "BLOCK")) CMD_READ_ALL_BLOCK();
  else CMD_Reply("ERR", ERR_PARAM);
  s_task = TASK_IDLE;
}

void CMD_READ_ALL_BLOCK(void) {
  if (!CMD_Begin(TASK_READ, 0)) return;
  CMD_EmitAllBlocks();
  s_task = TASK_IDLE;
}

void CMD_READ_BLOCK(char *block_id) {
  block_entry_t blk;
  uint16_t slot;
  char name[BLOCK_NAME_LEN + 1];
  uint16_t i, n = 0;
  if (!CMD_Begin(TASK_READ, 0)) return;
  slot = CMD_LoadBlockById(CMD_AtoU16(block_id), &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  CMD_GetBlockName(&blk, name);
  Uart1_SendString("DATA+");
  Uart1_SendNumber(blk.block_id);
  Uart1_SendString(";");
  Uart1_SendString(name);
  Uart1_SendString("|");
  for (i = 0; i < KVIDX_MAX; i++) {
    kv_entry_t e;
    uint16_t k;
    CMD_ReadKvEntry(i, &e);
    if (e.block_id != blk.block_id) continue;
    if (!(e.flags & KV_FLAG_VALID)) continue;
    if (n > 0) Uart1_SendString("|");
    for (k = 0; k < e.key_len; k++)
      Uart1_SendByte(EEPROM_ReadByte((uint16_t)(e.key_off + k)));
    n++;
  }
  Uart1_SendString("\r\n");
  s_task = TASK_IDLE;
}

void CMD_READ_BLOCK_NAME(char *block_name) {
  block_entry_t blk;
  uint16_t slot;
  char name[BLOCK_NAME_LEN + 1];
  if (!CMD_Begin(TASK_READ, 0)) return;
  slot = CMD_LoadBlockByName(block_name, &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  CMD_GetBlockName(&blk, name);
  Uart1_SendString("DATA+");
  Uart1_SendString(name);
  Uart1_SendString("?id=");
  Uart1_SendNumber(blk.block_id);
  Uart1_SendString("\r\n");
  s_task = TASK_IDLE;
}

void CMD_READ_KEY(char *block_id, char *key) {
  block_entry_t blk;
  uint16_t slot, idx;
  kv_entry_t e;
  uint16_t i, val_len, key_len;
  if (!CMD_Begin(TASK_READ, 0)) return;
  slot = CMD_LoadBlockById(CMD_AtoU16(block_id), &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  idx = CMD_FindKeyIdx(blk.block_id, key);
  if (idx == 0xFFFF) { CMD_Reply("ERR", ERR_KEY_NOT_FOUND); s_task = TASK_IDLE; return; }
  CMD_ReadKvEntry(idx, &e);
  key_len = CMD_ReadKvRecordLens(e.data_addr, &val_len);
  if (key_len == 0xFFFF) { CMD_Reply("ERR", ERR_KEY_NOT_FOUND); s_task = TASK_IDLE; return; }
  Uart1_SendString("DATA+");
  for (i = 0; i < val_len; i++)
    Uart1_SendByte(EEPROM_ReadByte((uint16_t)(e.data_addr + KV_OFF_KEY + key_len + i)));
  Uart1_SendString("\r\n");
  s_task = TASK_IDLE;
}

/* ---- WRITE ---- */

void CMD_WRITE(char *block_id, char *key, char *key_value) {
  block_entry_t blk;
  uint16_t slot, idx;
  uint16_t val_len;
  if (!CMD_Begin(TASK_WRITE, 1)) return;
  slot = CMD_LoadBlockById(CMD_AtoU16(block_id), &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  val_len = (uint16_t)strlen(key_value);
  idx = CMD_FindKeyIdx(blk.block_id, key);
  /* idx==0xFFFF 时 CMD_PutKeyValue 内部走新建路径 */
  if (CMD_PutKeyValue(&blk, slot, idx, key, (const uint8_t *)key_value, val_len) == ERR_OK)
    CMD_Reply("RESULT", ERR_OK);
  s_task = TASK_IDLE;
}

void CMD_WRITE_RAW(char *block_id, char *key, char *hex) {
  uint8_t bin[64];
  uint8_t n;
  block_entry_t blk;
  uint16_t slot, idx;
  if (!CMD_Begin(TASK_WRITE, 1)) return;
  slot = CMD_LoadBlockById(CMD_AtoU16(block_id), &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  n = CMD_HexToBytes(hex, bin, sizeof(bin));
  idx = CMD_FindKeyIdx(blk.block_id, key);
  if (CMD_PutKeyValue(&blk, slot, idx, key, bin, n) == ERR_OK)
    CMD_Reply("RESULT", ERR_OK);
  s_task = TASK_IDLE;
}

/* ---- CREATE ---- */

void CMD_CREATE_BLOCK(char *block_name, char *block_size) {
  /* 第二形参 block_size（USIZE/容量数值）当前按统一容量处理，仅接口兼容；
   * 用(void)标记意图（C275 已在文件头屏蔽） */
  (void)block_size;
  if (!CMD_Begin(TASK_CREATE, 1)) return;
  CMD_DoCreateBlock(block_name);
  s_task = TASK_IDLE;
}

void CMD_CREATE_KEY(char *block_flag, char *block_identifier, char *key, char *value) {
  if (!CMD_Begin(TASK_CREATE, 1)) return;
  if (CMD_DoCreateKey(block_flag, block_identifier, key, value)) CMD_Reply("RESULT", ERR_OK);
  s_task = TASK_IDLE;
}

/* ---- DELETE ---- */

void CMD_DELETE_BLOCK(char *block_id) {
  if (!CMD_Begin(TASK_DELETE, 1)) return;
  CMD_DoDeleteBlock(0, block_id, 1);
  s_task = TASK_IDLE;
}

void CMD_DELETE_BLOCK_NAME(char *block_name) {
  if (!CMD_Begin(TASK_DELETE, 1)) return;
  CMD_DoDeleteBlock("0", block_name, 0);
  s_task = TASK_IDLE;
}

void CMD_DELETE_KEY(char *block_flag, char *block_identifier, char *key) {
  block_entry_t blk;
  uint16_t slot, idx;
  if (!CMD_Begin(TASK_DELETE, 1)) return;
  slot = CMD_ResolveBlock(block_flag, block_identifier, &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  idx = CMD_FindKeyIdx(blk.block_id, key);
  if (idx == 0xFFFF) { CMD_Reply("ERR", ERR_KEY_NOT_FOUND); s_task = TASK_IDLE; return; }
  CMD_FreeKvSlot(idx);
  if (blk.kv_count > 0) blk.kv_count--;
  CMD_WriteBlockSlot(slot, &blk);
  CMD_Reply("RESULT", ERR_OK);
  s_task = TASK_IDLE;
}

/* ---- UPDATE ---- */

void CMD_UPDATE_BLOCK(char *block_id, char *new_name) {
  block_entry_t blk;
  uint16_t slot;
  if (!CMD_Begin(TASK_UPDATE, 1)) return;
  slot = CMD_LoadBlockById(CMD_AtoU16(block_id), &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  if (new_name && strlen(new_name) > 0) {
    CMD_SetBlockName(&blk, new_name);
    EEPROM_SetAddress(IC_0_24C64);
    CMD_WriteBlockSlot(slot, &blk);
  }
  CMD_Reply("RESULT", ERR_OK);
  s_task = TASK_IDLE;
}

void CMD_UPDATE_KEY(char *block_id, char *key, char *key_value) {
  block_entry_t blk;
  uint16_t slot, idx;
  uint16_t val_len;
  if (!CMD_Begin(TASK_UPDATE, 1)) return;
  slot = CMD_LoadBlockById(CMD_AtoU16(block_id), &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  idx = CMD_FindKeyIdx(blk.block_id, key);
  if (idx == 0xFFFF) { CMD_Reply("ERR", ERR_KEY_NOT_FOUND); s_task = TASK_IDLE; return; }
  val_len = (uint16_t)strlen(key_value);
  if (CMD_PutKeyValue(&blk, slot, idx, key, (const uint8_t *)key_value, val_len) != ERR_OK) {
    s_task = TASK_IDLE; return;
  }
  CMD_Reply("RESULT", ERR_OK);
  s_task = TASK_IDLE;
}

/* ---- GET ALL ---- */

void CMD_GET_ALL_BLOCK(void) {
  if (!CMD_Begin(TASK_GET_ALL, 0)) return;
  CMD_EmitAllBlocks();
  s_task = TASK_IDLE;
}

void CMD_GET_ALL_KEY(char *block_id) {
  block_entry_t blk;
  uint16_t slot;
  char name[BLOCK_NAME_LEN + 1];
  if (!CMD_Begin(TASK_GET_ALL, 0)) return;
  slot = CMD_LoadBlockById(CMD_AtoU16(block_id), &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  CMD_GetBlockName(&blk, name);
  Uart1_SendString("DATA+[");
  Uart1_SendString(name);
  Uart1_SendString(";");
  Uart1_SendNumber(blk.block_id);
  Uart1_SendString("](");
  CMD_EmitBlockKv(blk.block_id);
  Uart1_SendString(")\r\n");
  s_task = TASK_IDLE;
}

void CMD_GET_SIZE(void) {
  s_task = TASK_GET_ALL;
  Uart1_SendString("DATA+");
  Uart1_SendNumber(DEV_EEPROM_SIZE);
  Uart1_SendString("+");
  Uart1_SendNumber(CMD_ReadU16(CFG_ADDR_DATA_TOTAL));
  Uart1_SendString("+");
  Uart1_SendNumber(CMD_ReadU16(CFG_ADDR_DATA_USED));
  Uart1_SendString("+");
  Uart1_SendNumber(CMD_ReadU16(CFG_ADDR_DATA_FREE));
  Uart1_SendString("\r\n");
  s_task = TASK_IDLE;
}

/* ---- FORMAT / maintenance ---- */

void CMD_FORMAT_DEV(void) {
  if (!CMD_Begin(TASK_FORMAT, 1)) return;
  StorageMgr_Format_BlockZone();
  StorageMgr_Format_EEPROMZone();
  CMD_Reply("RESULT", ERR_OK);
  s_task = TASK_IDLE;
}

void CMD_FORMAT_BLOCK(char *block_flag, char *block_identifier) {
  block_entry_t blk;
  uint16_t slot;
  if (!CMD_Begin(TASK_FORMAT, 1)) return;
  slot = CMD_ResolveBlock(block_flag, block_identifier, &blk);
  if (slot == 0xFFFF) { CMD_Reply("ERR", ERR_BLOCK_NOT_FOUND); s_task = TASK_IDLE; return; }
  CMD_PurgeBlockKv(blk.block_id);
  EEPROM_SetAddress(IC_0_24C64);
  blk.kv_count = 0;
  CMD_WriteBlockSlot(slot, &blk);
  CMD_Reply("RESULT", ERR_OK);
  s_task = TASK_IDLE;
}

/* ---- Main AT dispatcher ---- */

void CMD_Parser(char *cmd) {
  char *tokens[10];
  int tc = CMD_Split(cmd, '+', tokens, 10);
  int i;
  char *a2, *a3, *a4, *a5, *a6;

  if (tc < 2) return;
  if (!CMD_Compare(tokens[0], "AT")) return;

  for (i = 2; i < tc; i++) tokens[i] = CMD_StripQuote(tokens[i]);
  a2 = (tc >= 3) ? tokens[2] : 0;
  a3 = (tc >= 4) ? tokens[3] : 0;
  a4 = (tc >= 5) ? tokens[4] : 0;
  a5 = (tc >= 6) ? tokens[5] : 0;
  a6 = (tc >= 7) ? tokens[6] : 0;

  if (CMD_Compare(tokens[1], "INIT")) {
    if (tc >= 5)
      CMD_INIT(a2, a3, CMD_Atoi(a4), (tc >= 6) ? CMD_Atoi(a5) : DATA_SIZE_24C64);
    else
      CMD_Reply("ERR", ERR_PARAM);
    return;
  }
  if (CMD_Compare(tokens[1], "ECHO")) { CMD_ECHO(); return; }
  if (CMD_Compare(tokens[1], "INFO")) { CMD_INFO(); return; }
  if (CMD_Compare(tokens[1], "STATUS")) { CMD_STATUS(); return; }
  if (CMD_Compare(tokens[1], "USAGE")) { CMD_USAGE(); return; }

  if (CMD_Compare(tokens[1], "VERSION")) { CMD_VERSION(); return; }

  if (CMD_Compare(tokens[1], "AUTH")) {
    if (a2 && a3 && CMD_Compare(a2, "PASSWORD")) {
      if (CMD_Compare(a3, "CREATE") && a4) { CMD_AUTH_CREATE(a4); return; }
      if (CMD_Compare(a3, "VERIFY") && a4) { CMD_AUTH_VERIFY(a4); return; }
      if (CMD_Compare(a3, "ENABLE")) { CMD_AUTH_ENABLE(); return; }
      if (CMD_Compare(a3, "DISABLE")) { CMD_AUTH_DISABLE(); return; }
      if (CMD_Compare(a3, "VERIFYOUT")) { CMD_AUTH_VERIFYOUT(); return; }
      if (CMD_Compare(a3, "CHANGE") && a4) { CMD_AUTH_CHANGE(a4, a5); return; }
      if (CMD_Compare(a3, "RESET") && a4) { CMD_AUTH_RESET(a4); return; }
    }
    CMD_Reply("ERR", ERR_PARAM);
    return;
  }

  if (CMD_Compare(tokens[1], "READ")) {
    if (a2 && CMD_Compare(a2, "BLOCK")) {
      if (a3 && CMD_Compare(a3, "NAME") && a4) { CMD_READ_BLOCK_NAME(a4); return; }
      if (a3) { CMD_READ_BLOCK(a3); return; }
      CMD_READ_ALL_BLOCK(); return;
    }
    if (a2 && CMD_Compare(a2, "KEY") && a3 && a4) { CMD_READ_KEY(a3, a4); return; }
    CMD_READ(a2);
    return;
  }

  if (CMD_Compare(tokens[1], "WRITE")) {
    if (a2 && CMD_Compare(a2, "RAW") && a3 && a4 && a5) { CMD_WRITE_RAW(a3, a4, a5); return; }
    if (a2 && a3 && a4) { CMD_WRITE(a2, a3, a4); return; }
    CMD_Reply("ERR", ERR_PARAM);
    return;
  }

  if (CMD_Compare(tokens[1], "CREATE")) {
    if (a2 && CMD_Compare(a2, "BLOCK") && a3 && a4) { CMD_CREATE_BLOCK(a3, a4); return; }
    if (a2 && CMD_Compare(a2, "KEY") && a3 && a4 && a5 && a6) { CMD_CREATE_KEY(a3, a4, a5, a6); return; }
    CMD_Reply("ERR", ERR_PARAM);
    return;
  }

  if (CMD_Compare(tokens[1], "DELETE")) {
    if (a2 && CMD_Compare(a2, "BLOCK")) {
      if (a3 && CMD_Compare(a3, "NAME") && a4) { CMD_DELETE_BLOCK_NAME(a4); return; }
      if (a3) { CMD_DELETE_BLOCK(a3); return; }
    }
    if (a2 && CMD_Compare(a2, "KEY") && a3 && a4 && a5) { CMD_DELETE_KEY(a3, a4, a5); return; }
    CMD_Reply("ERR", ERR_PARAM);
    return;
  }

  if (CMD_Compare(tokens[1], "UPDATE")) {
    if (a2 && CMD_Compare(a2, "BLOCK") && a3) { CMD_UPDATE_BLOCK(a3, a4); return; }
    if (a2 && CMD_Compare(a2, "KEY") && a3 && a4 && a5) { CMD_UPDATE_KEY(a3, a4, a5); return; }
    CMD_Reply("ERR", ERR_PARAM);
    return;
  }

  if (CMD_Compare(tokens[1], "GET")) {
    if (a2 && CMD_Compare(a2, "ALL") && a3) {
      if (CMD_Compare(a3, "BLOCK")) { CMD_GET_ALL_BLOCK(); return; }
      if (CMD_Compare(a3, "KEY") && a4) { CMD_GET_ALL_KEY(a4); return; }
    }
    if (a2 && CMD_Compare(a2, "SIZE")) { CMD_GET_SIZE(); return; }
    CMD_Reply("ERR", ERR_PARAM);
    return;
  }

  if (CMD_Compare(tokens[1], "FORMAT")) {
    if (a2 && CMD_Compare(a2, "DEV")) { CMD_FORMAT_DEV(); return; }
    if (a2 && CMD_Compare(a2, "BLOCK") && a3 && a4) { CMD_FORMAT_BLOCK(a3, a4); return; }
    CMD_Reply("ERR", ERR_PARAM);
    return;
  }

  CMD_Reply("ERR", ERR_PARAM);
}