#ifndef __SERIAL_H__
#define __SERIAL_H_

#include "AI8G.h"

void Uart1_Init(void);
void Uart1_SendByte(unsigned char byte);
void Uart1_SendString(uint8_t *str);
void Uart1_SendHexBuffer(uint8_t *buf, uint16_t len);
void Uart1_SendNumber(uint16_t num);
void Uart1_SendHex(uint8_t byte);
void Uart1_ReceiveByte(void);

uint8_t Uart1_ReceiveString(uint8_t *buf, uint8_t max_len, uint16_t timeout);

#endif // __SERIAL_H__
