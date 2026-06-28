#ifndef __HW_24C64_H__
#define __HW_24C64_H__
#include "AI8G.h"

#define IC0_WP P54
#define IC0_WP P55

uint8_t EEPROM_ADDR = 0xA0; // 24C64设备地址

// 函数声明
void HW_I2C_Init(void);
void HW_I2C_Start(void);
void HW_I2C_Stop(void);
void HW_I2C_SendByte(uint8_t byte);
uint8_t HW_I2C_ReceiveByte(void);
bit HW_I2C_WaitAck(void);
void HW_I2C_SendAck(bit ack);

// 24C64操作函数
void HW_EEPROM_WriteByte(uint16_t addr, uint8_t dat);
uint8_t HW_EEPROM_ReadByte(uint16_t addr);
void HW_EEPROM_WriteString(uint16_t addr, uint8_t *str);
void HW_EEPROM_ReadString(uint16_t addr, uint8_t *str, uint16_t len);
void HW_EEPROM_ReadBuffer(uint16_t addr, uint8_t *buffer, uint16_t len);
void HW_EEPROM_ClearAll(void);
void HW_EEPROM_SetAddress(uint16_t addr);

#endif // __HW_24C64_H__