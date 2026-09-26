#include "StorageMgr.h"
#include <string.h>

/* 算法属性表: 索引即 ENC_ALGO_*, 驱动长度计算与加解密 */
static const enc_algo_info_t code enc_algo_tab[] = {
  /* algo                 mode                iv  tag  blk  keep */
  { ENC_ALGO_NONE,        ENC_MODE_STREAM,     0,  0,  1,  1 },
  { ENC_ALGO_AES128_CBC,  ENC_MODE_BLOCK_PAD, 16,  0, 16,  0 },
  { ENC_ALGO_AES128_GCM,  ENC_MODE_AEAD,      12, 16,  1,  0 },
  { ENC_ALGO_XOR,         ENC_MODE_STREAM,     0,  0,  1,  1 },
  { ENC_ALGO_CAESAR,      ENC_MODE_STREAM,     0,  0,  1,  1 },
  { ENC_ALGO_RC4,         ENC_MODE_STREAM,     0,  0,  1,  1 },
};

#define ENC_ALGO_CNT (sizeof(enc_algo_tab) / sizeof(enc_algo_tab[0]))

// 第二块容量代码 -> 总字节数
static uint16_t StorageMgr_SizeCodeToBytes(uint8_t size_code) {
  switch (size_code) {
    case DATA_SIZE_24C32:  return 4  * 1024;
    case DATA_SIZE_24C64:  return 8  * 1024;
    case DATA_SIZE_24C128: return 16 * 1024;
    case DATA_SIZE_24C256: return 32 * 1024;
    case DATA_SIZE_24C512: return 64 * 1024;
    default:               return 0;
  }
}

// 取算法属性, 越界回退到明文
static const enc_algo_info_t *StorageMgr_AlgoInfo(uint8_t algo) {
  if (algo >= ENC_ALGO_CNT) algo = ENC_ALGO_NONE;
  return &enc_algo_tab[algo];
}

// 写 16 位小端
static void StorageMgr_WriteU16(uint16_t addr, uint16_t val) {
  EEPROM_WriteByte(addr, (uint8_t)(val & 0xFF));
  EEPROM_WriteByte(addr + 1, (uint8_t)(val >> 8));
}

// 读 16 位小端
static uint16_t StorageMgr_ReadU16(uint16_t addr) {
  return (uint16_t)EEPROM_ReadByte(addr) |
         ((uint16_t)EEPROM_ReadByte(addr + 1) << 8);
}

// 初始化存储器管理器
void StorageMgr_Init(void) {
  I2C_Init();
}

// 初始化设备: 清零第一块并写入配置区, 返回 ERR_*
int StorageMgr_InitDevice(const char *name, const char *pass, uint8_t data_size_code) {
  uint16_t total;
  uint8_t flags = FLAG_INITIALIZED;

  total = StorageMgr_SizeCodeToBytes(data_size_code);
  if (total == 0) return ERR_SIZE_CODE_INVALID;

  if (pass != NULL && strlen(pass) > 0 && strcmp(pass, PASS_NONE_STR) != 0)
    flags |= FLAG_PWD_AUTH;

    EEPROM_SetAddress(IC_0_24C64);
  EEPROM_Fill(0, DEV_EEPROM_SIZE, 0x00);

  EEPROM_WriteByte(CFG_ADDR_FLAGS, flags);
  EEPROM_WriteByte(CFG_ADDR_ENC_ALGO, ENC_ALGO_NONE);
  EEPROM_WriteByte(CFG_ADDR_KEY_VOL, KEY_VOL_DEFAULT);
  EEPROM_WriteByte(CFG_ADDR_DEV_SIZE, DEV_SIZE_DEFAULT);

  EEPROM_WriteByte(CFG_ADDR_DATA_SLAVE, IC_1_24CXX);
  EEPROM_WriteByte(CFG_ADDR_DATA_SIZE_CODE, data_size_code);
  StorageMgr_WriteU16(CFG_ADDR_DATA_TOTAL, total);
  StorageMgr_WriteU16(CFG_ADDR_DATA_USED, 0);
  StorageMgr_WriteU16(CFG_ADDR_DATA_FREE, total);
  StorageMgr_WriteU16(CFG_ADDR_DATA_HWM, DATA_HEAD_SIZE);
  StorageMgr_WriteU16(CFG_ADDR_DATA_NEXT, DATA_HEAD_SIZE);
  EEPROM_WriteByte(CFG_ADDR_DATA_USAGE, 0);

  EEPROM_WriteByte(CFG_ADDR_BITMAP_FMT, BITMAP_FMT_VER);
  StorageMgr_WriteU16(CFG_ADDR_BITMAP_OFF, FIRST_BLOCK_BITMAP);
  StorageMgr_WriteU16(CFG_ADDR_BITMAP_LEN, (uint16_t)(total / DATA_ALLOC_UNIT / 8));
  StorageMgr_WriteU16(CFG_ADDR_BLOCKTAB_OFF, FIRST_BLOCK_BLOCKTAB);
  StorageMgr_WriteU16(CFG_ADDR_KVIDX_OFF, FIRST_BLOCK_KVIDX);
  StorageMgr_WriteU16(CFG_ADDR_KVIDX_CNT, 0);
  EEPROM_WriteByte(CFG_ADDR_KVIDX_ENTRY, (uint8_t)sizeof(kv_entry_t));

  StorageMgr_InitDataHead(total, data_size_code);
  return ERR_OK;
}

// 初始化第二块数据片头
void StorageMgr_InitDataHead(uint16_t total, uint8_t size_code) {
  uint8_t sum;

  EEPROM_SetAddress(IC_1_24CXX);
  EEPROM_WriteByte(0x00, DATA_HEAD_MAGIC);
  EEPROM_WriteByte(0x01, DATA_HEAD_VER);
  EEPROM_WriteByte(0x02, size_code);
  StorageMgr_WriteU16(0x03, total);
  StorageMgr_WriteU16(0x05, DATA_ALLOC_UNIT * 2);
  StorageMgr_WriteU16(0x07, 0);
  StorageMgr_WriteU16(0x09, DATA_HEAD_SIZE);
  StorageMgr_WriteU16(0x0B, DATA_HEAD_SIZE);
  EEPROM_WriteByte(0x0D, 0);
  EEPROM_WriteByte(0x0E, 0);
  sum = (uint8_t)(DATA_HEAD_MAGIC ^ DATA_HEAD_VER ^ size_code);
  sum ^= (uint8_t)(total & 0xFF) ^ (uint8_t)(total >> 8);
  EEPROM_WriteByte(0x0F, sum);
}

// 按算法计算密文长度(含 TAG)
uint16_t enc_calc_cipher_len(uint8_t algo, uint16_t plain_len) {
  const enc_algo_info_t *info = StorageMgr_AlgoInfo(algo);
  uint16_t len = plain_len;

  if (info->mode == ENC_MODE_BLOCK_PAD)
    len = (uint16_t)((len / info->block_size + 1) * info->block_size);
  return (uint16_t)(len + info->tag_len);
}

// 按算法反推明文长度
uint16_t enc_calc_plain_len(uint8_t algo, uint16_t enc_len) {
  const enc_algo_info_t *info = StorageMgr_AlgoInfo(algo);

  if (info->tag_len > enc_len) return 0;
  return (uint16_t)(enc_len - info->tag_len);
}

// 格式化片内块区(块目录/索引/位图)
void StorageMgr_Format_BlockZone(void) {
    EEPROM_SetAddress(IC_0_24C64);
  EEPROM_Fill(FIRST_BLOCK_BLOCKTAB, FIRST_BLOCK_BITMAP + DATA_ALLOC_UNIT - FIRST_BLOCK_BLOCKTAB, 0x00);
}

// 整片清零第一块
void StorageMgr_Format_EEPROMZone(void) {
  EEPROM_SetAddress(IC_0_24C64);
  HW_EEPROM_ClearAll();
}
