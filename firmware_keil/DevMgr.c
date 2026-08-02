#include "DevMgr.h"
#include "StorageMgr.h"



void DM_format_block(uint16_t size,uint8_t byte) {
    while (size--) {
        EEPROM_WriteByte(size, byte);
    }
}


/* 接入计数 +1 (0x22~0x23 : 2 Byte uint16 大端存储, 最大65535封顶) */
void DM_UPDATECOUNT_Add(void) {
    uint16_t count = ((uint16_t)EEPROM_ReadByte(CFG_ADDR_ACCESS_CNT) << 8) |
                     (uint16_t)EEPROM_ReadByte(CFG_ADDR_ACCESS_CNT + 1);

    if (count < 0xFFFF) count++;   /* 达到65535后不再自增, 避免溢出回绕 */

    EEPROM_WriteByte(CFG_ADDR_ACCESS_CNT,     (uint8_t)(count >> 8));    /* 高字节(0x22) */
    EEPROM_WriteByte(CFG_ADDR_ACCESS_CNT + 1, (uint8_t)(count & 0xFF));  /* 低字节(0x23) */
}

/* 读取接入计数 (0x22~0x23 : 2 Byte uint16 大端) */
uint16_t DM_UPDATECOUNT_Get(void) {
    return ((uint16_t)EEPROM_ReadByte(CFG_ADDR_ACCESS_CNT) << 8) |
           (uint16_t)EEPROM_ReadByte(CFG_ADDR_ACCESS_CNT + 1);
}