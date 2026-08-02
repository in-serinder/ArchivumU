#ifndef __HW_24C64_H__
#define __HW_24C64_H__
#include "AI8G.h"


// sbit SDA = P3 ^ 3;
// sbit SCL = P3 ^ 2;

#define SDA P33
#define SCL P32

#define IC0_WP  P54
#define IC1_WP  P55

extern uint8_t EEPROM_ADDR;   // 8位I2C写地址：#0=0xA0, #1=0xA2
extern uint8_t I2C_Err;       // 0=OK, 1=DevAddrNACK 2=AddrHiNACK 3=AddrLoNACK 4=DataNACK 5=ReadAddrNACK

void I2C_Init(void);
void I2C_Start(void);
void I2C_Stop(void);
void I2C_SendByte(uint8_t byte);
uint8_t I2C_ReceiveByte(void);
bit I2C_WaitAck(void);
void I2C_SendAck(bit ack);

void EEPROM_WriteByte(uint16_t addr, uint8_t dat);
uint8_t EEPROM_ReadByte(uint16_t addr);
void EEPROM_SetAddress(uint16_t addr);  // 入参:7位设备地址(0x50/#0, 0x51/#1)，内部左移1位成8位写地址

void EEPROM_WriteString(uint16_t addr, uint8_t *str);  // 从addr起始连续写入字符串(含结尾'\0')
uint8_t EEPROM_ReadString(uint16_t addr, uint8_t *buf, uint8_t maxLen);  // 从addr起始读字符串，返回字符数(不含'\0')

void HW_EEPROM_ClearAll(void);  // 整片24C64清零(格式化EEPROM)

/* main.c 兼容宏 */
#define HW_I2C_Init          I2C_Init
#define HW_EEPROM_WriteByte  EEPROM_WriteByte
#define HW_EEPROM_ReadByte   EEPROM_ReadByte
#define HW_EEPROM_SetAddress EEPROM_SetAddress
#define HW_EEPROM_WriteString EEPROM_WriteString
#define HW_EEPROM_ReadString  EEPROM_ReadString

#endif /* __HW_24C64_H__ */