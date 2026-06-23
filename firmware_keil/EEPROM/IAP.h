#ifndef __IAP_H__
#define __IAP_H__

#include "AI8G.h"

// IAP命令
#define IAP_CMD_READ 0x01  // 读字节
#define IAP_CMD_PROG 0x02  // 写字节
#define IAP_CMD_ERASE 0x03 // 擦除扇区

// IAP使能位
#define IAP_EN 0x80

// STC8G1K08A EEPROM相关常量
#define EEPROM_START_ADDR 0x0000 // EEPROM起始地址
#define EEPROM_END_ADDR 0x03FF   // EEPROM结束地址(1KB)
#define EEPROM_SIZE 1024         // EEPROM大小(字节)

// IAP功能函数
void IAP_Enable(uint8_t sys_clk_mhz);
void IAP_Disable(void);

// EEPROM基本读写函数
uint8_t EEPROM_ReadByte(uint16_t addr);
bit EEPROM_ReadBit(uint16_t addr, uint8_t bit_pos);
void EEPROM_WriteByte(uint16_t addr, uint8_t data);
void EEPROM_WriteBit(uint16_t addr, uint8_t bit_pos, bit bit_value);

// EEPROM批量读写函数
uint16_t EEPROM_ReadBytes(uint16_t addr, uint16_t len, uint8_t *buf);
uint16_t EEPROM_ReadRange(uint16_t start_addr, uint16_t end_addr, uint8_t *buf);
uint16_t EEPROM_ReadAll(uint8_t *buf);
uint16_t EEPROM_WriteBytes(uint16_t addr, uint16_t len, const uint8_t *buf);

// EEPROM擦除与清空函数
void EEPROM_EraseSector(uint16_t sector_addr);
void EEPROM_EraseAll(void);
uint16_t EEPROM_Fill(uint16_t addr, uint16_t len, uint8_t value);
uint16_t EEPROM_Clear(uint16_t addr, uint16_t len);
void EEPROM_ClearAll(void);

// EEPROM辅助功能函数
bit EEPROM_CheckValue(uint16_t addr, uint16_t len, uint8_t value);
bit EEPROM_Compare(uint16_t addr, uint16_t len, const uint8_t *buf);
void EEPROM_ToggleBit(uint16_t addr, uint8_t bit_pos);

#endif // __IAP_H__