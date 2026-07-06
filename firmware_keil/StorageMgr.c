#include "StorageMgr.h"

// 初始化存储器管理器
void StorageMgr_Init(void) {
  HW_I2C_Init();
  //   IAP_Enable(16);
}

// 初始化片内配置区
void StorageMgr_Init_ConfigZone(void) {
  // 初始化配置区
}
// 格式化片内块区
void StorageMgr_Format_BlockZone(void) {
  // 格式化块区
}

// 格式化24c64 EEPROM内存储区
void StorageMgr_Format_EEPROMZone(void) {
  // 格式化EEPROM
  HW_EEPROM_ClearAll();
}