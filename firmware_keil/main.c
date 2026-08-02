#include "CommandParser.h"
#include "DevMgr.h"
#include "StorageMgr.h"
#include "BLAKE2s.h"
#include "Serial.h"

#define FIRMWARE_NAME "ArchivumU"
#define FIRMWARE_VERSION "1.0.0"
#define FIRMWARE_DATE "2026-08-01"
#define FIRMWARE_BLOCK_ZONE_SIZE 16*1024 // 16KB block
#define FIRMWARE_STORAGE_ZONE_SIZE 16*1024 // 16KB block

uint8_t tmp[16];

void main(void) {
  uint16_t update_cnt;
  Uart1_Init();
  CMD_PARSER_SET_STORAGE(FIRMWARE_STORAGE_ZONE_SIZE);
  BLAKE2s_Checksum("123456", 6, tmp); /* 临时: 测量代码尺寸 */
  Uart1_SendHexBuffer(tmp, 16);
  update_cnt = DM_UPDATECOUNT_Get();
  Uart1_SendHexBuffer((uint8_t *)&update_cnt, 2);
  Uart1_SendString("\r\n");

  while (1) {
  }
}