#include "Serial.h"

void Uart1_Init(void) // 115200bps@22.1184MHz
{
  SCON = 0x50;  // 8位数据,可变波特率
  AUXR |= 0x40; // 定时器时钟1T模式
  AUXR &= 0xFE; // 串口1选择定时器1为波特率发生器
  TMOD &= 0x0F; // 设置定时器模式
  TL1 = 0xD0;   // 设置定时初始值
  TH1 = 0xFF;   // 设置定时初始值
  ET1 = 0;      // 禁止定时器中断
  TR1 = 1;      // 定时器1开始计时
}

void Uart1_SendByte(uint8_t byte) {
  SBUF = byte;
  while (!TI)
    ;
  TI = 0;
}

/**
 * @brief 发送字符串
 * @param str 指向字符串的指针
 */
void Uart1_SendString(uint8_t *str) {
  while (*str != '\0') {
    Uart1_SendByte(*str++);
  }
}

/**
 * @brief 发送单个字节的十六进制表示
 * @param byte 要发送的字节
 */
void Uart1_SendHex(uint8_t byte) {
  uint8_t high = (byte >> 4) & 0x0F;
  uint8_t low = byte & 0x0F;

  // 发送高4位
  if (high < 10) {
    Uart1_SendByte('0' + high);
  } else {
    Uart1_SendByte('A' + high - 10);
  }

  // 发送低4位
  if (low < 10) {
    Uart1_SendByte('0' + low);
  } else {
    Uart1_SendByte('A' + low - 10);
  }
}

/**
 * @brief 发送数据缓冲区的十六进制表示
 * @param buf 数据缓冲区指针
 * @param len 数据长度
 */
void Uart1_SendHexBuffer(uint8_t *buf, uint16_t len) {
  uint16_t i;
  for (i = 0; i < len; i++) {
    Uart1_SendHex(buf[i]);
    Uart1_SendByte(' '); // 每个字节之间用空格分隔
  }
}

/**
 * @brief 发送无符号整数
 * @param num 要发送的无符号整数
 */
void Uart1_SendNumber(uint16_t num) {
  uint8_t buf[5];
  uint8_t i = 0;

  if (num == 0) {
    Uart1_SendByte('0');
    return;
  }

  while (num > 0) {
    buf[i++] = num % 10 + '0';
    num /= 10;
  }

  while (i > 0) {
    Uart1_SendByte(buf[--i]);
  }
}

/**
 * @brief 接收单个字节（阻塞方式）
 * @return 接收到的字节
 */
uint8_t Uart1_ReceiveByte(void) {
  while (!RI)
    ;
  RI = 0;
  return SBUF;
}

/**
 * @brief 接收字符串（带超时检测）
 * @param buf 接收缓冲区
 * @param max_len 最大接收长度
 * @param timeout 超时时间（单位：ms），0表示无限等待
 * @return 实际接收到的字符数，超时返回0
 */
uint8_t Uart1_ReceiveString(uint8_t *buf, uint8_t max_len, uint16_t timeout) {
  uint8_t len = 0;
  uint8_t ch;
  uint16_t t,i = 0;

  while (len < max_len - 1) {
    if (RI) {
      RI = 0;
      ch = SBUF;

      // 检测换行符作为结束标志
      if (ch == '\n' || ch == '\r') {
        break;
      }

      buf[len++] = ch;
      t = 0; // 重置超时计时
    } else {
      if (timeout > 0) {
        t++;
        if (t >= timeout) {
          break; // 超时退出
        }
        // 简单延时约1ms
        for ( i = 0; i < 1000; i++)
          ;
      }
    }
  }

  buf[len] = '\0'; // 添加字符串结束符
  return len;
}