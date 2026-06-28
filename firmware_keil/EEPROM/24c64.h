#ifndef __24C64_H__
#define __24C64_H__

#include "AI8G.h"

// 引脚定义
#define SDA P33
#define SCL P32
// 处于11上

// 24C64相关定义
uint8_t EEPROM_ADDR = 0xA0; // 24C64设备地址

// 函数声明
void I2C_Init(void);
void I2C_Start(void);
void I2C_Stop(void);
void I2C_SendByte(unsigned char byte);
unsigned char I2C_ReceiveByte(void);
bit I2C_WaitAck(void);
void I2C_SendAck(bit ack);

// 24C64操作函数
void EEPROM_WriteByte(unsigned int addr, unsigned char dat);
unsigned char EEPROM_ReadByte(unsigned int addr);
void EEPROM_WriteString(unsigned int addr, unsigned char *str);
void EEPROM_ReadString(unsigned int addr, unsigned char *str, unsigned int len);
void EEPROM_ReadBuffer(unsigned int addr, unsigned char *buffer,
                       unsigned int len);
void EEPROM_ClearAll(void);

// 内部切地址
void EEPROM_SetAddress(unsigned int addr) { EEPROM_ADDR = addr; }

#endif // __24C64_H__
