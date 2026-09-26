#include "CommandParser.h"
#include "ConfigMgr.h"
#include "StorageMgr.h"
#include "FeatTag.h"
#include "Serial.h"

#define FIRMWARE_NAME "ArchivumU"
#define FIRMWARE_VERSION "1.0.0"
#define FIRMWARE_DATE "2026-08-01"
/* 串口行缓冲: 存放一条以 \r 或 \n 结束的 AT 指令 */
static uint8_t cmd_buf[64];

void main(void) {
  Uart1_Init();
  StorageMgr_Init();

  while (1) {
    /* 阻塞接收一行指令(超时返回0), 收到后交给解析器分发 */
    if (Uart1_ReceiveString(cmd_buf, sizeof(cmd_buf), 0) > 0) {
      CMD_Parser((char *)cmd_buf);
    }
  }
}
