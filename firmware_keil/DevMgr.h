#ifndef __DEV_MGR_H__
#define __DEV_MGR_H__

#include "AI8G.h"
#include "HW_24C64.h"

void DM_format_block(uint16_t size,uint8_t byte);
void DM_UPDATECOUNT_Add(void);      /* 接入计数+1 (0x22~0x23 uint16大端, 最大65535封顶) */
uint16_t DM_UPDATECOUNT_Get(void);  /* 读取接入计数 (0x22~0x23 uint16大端) */

#endif // __DEV_MGR_H__
