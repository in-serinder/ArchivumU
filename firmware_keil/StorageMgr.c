#include "StorageMgr.h"

/* ============================================================
 * StorageMgr —— 双 EEPROM 布局层
 * ------------------------------------------------------------
 * 只负责第一片(0x50 元数据)/第二片(0x51 数据)底层布局：
 *   - 配置区各字段的初始化
 *   - 数据片头(自描述)初始化
 *   - 块区 / 整片格式化
 * 不含加密、不含串口应答、不含鉴权。
 * ============================================================ */

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
// 密码摘要与设备名由 ConfigMgr 负责，这里只落盘布局裸字段（形参仅接口兼容）。
int StorageMgr_InitDevice(const char *name, const char *pass, uint8_t data_size_code) {
  uint16_t total;

  /* 形参 name / pass 仅保持接口兼容，本层不处理，用(void)标记意图（C275 已在文件头屏蔽） */
  (void)name;
  (void)pass;
  total = StorageMgr_SizeCodeToBytes(data_size_code);
  if (total == 0) return ERR_SIZE_CODE_INVALID;

  EEPROM_SetAddress(IC_0_24C64);
  EEPROM_Fill(0, DEV_EEPROM_SIZE, 0x00);

  EEPROM_WriteByte(CFG_ADDR_FLAGS, FLAG_INITIALIZED);
  EEPROM_WriteByte(CFG_ADDR_ENC_ALGO, ENC_ALGO_NONE);
  EEPROM_WriteByte(CFG_ADDR_KEY_VOL, KEY_VOL_DEFAULT);
  EEPROM_WriteByte(CFG_ADDR_DEV_SIZE, (uint8_t)(EEPROM0_CAPACITY_BYTES / 1024));

  EEPROM_WriteByte(CFG_ADDR_DATA_SLAVE, IC_1_24CXX);
  EEPROM_WriteByte(CFG_ADDR_DATA_SIZE_CODE, data_size_code);
  StorageMgr_WriteU16(CFG_ADDR_DATA_TOTAL, total);
  StorageMgr_WriteU16(CFG_ADDR_DATA_PAGE, DATA_ALLOC_UNIT * 2);
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

// 格式化片内块区(块目录/索引/位图)
void StorageMgr_Format_BlockZone(void) {
  EEPROM_SetAddress(IC_0_24C64);
  EEPROM_Fill(FIRST_BLOCK_BLOCKTAB,
              (uint16_t)(FIRST_BLOCK_BITMAP + DATA_ALLOC_UNIT - FIRST_BLOCK_BLOCKTAB),
              0x00);
}

// 整片清零第一块
void StorageMgr_Format_EEPROMZone(void) {
  EEPROM_SetAddress(IC_0_24C64);
  HW_EEPROM_ClearAll();
}
