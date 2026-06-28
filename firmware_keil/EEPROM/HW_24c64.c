#include "HW_24c64.h"

// 延时函数
void HW_I2C_Delay(void) {
  uint8_t i = 10;
  while (i--)
    ;
}

// 初始化硬件I2C
void HW_I2C_Init(void) {
  // 1. 开启扩展特殊寄存器访问 EAXFR=1(P_SW2.7)
  P_SW2 |= 0x80;

  // 2. I2C引脚选择：bit5 bit4 = 11 → SCL=P3.2，SDA=P3.3
  P_SW2 |= 0x30;

  // 3. IO配置为 开漏模式：M1=1，M0=0
  P3M1 |= (1 << 2) | (1 << 3);
  P3M0 &= ~((1 << 2) | (1 << 3));

  // 4. 配置I2C外设：使能+主机模式+分频
  // BIT7=1(I2C_EN), BIT6=1(主机), MSSPEED分频值
  // 22.1184M MSSPEED=13 → 400kHz
  I2CCFG = 0xC0 | 13;

  // 5. 总线复位，释放SDA/SCL
  I2CMSST = 0x08;

  // 操作完可关闭扩展SFR，不影响后续使用
  P_SW2 &= ~0x80;
}

// 发送起始信号
void HW_I2C_Start(void) {
  P_SW2 |= 0x80;
  // 发送START条件
  I2CMSCR = 0x01; // STA=1
  // 等待START发送完成
  while (!(I2CMSST & 0x20))
    ;               // 等待SB=1
  I2CMSST &= ~0x20; // 清除SB标志
  P_SW2 &= ~0x80;
}

// 发送停止信号
void HW_I2C_Stop(void) {
  P_SW2 |= 0x80;
  // 发送STOP条件
  I2CMSCR = 0x04; // STO=1
  // 等待STOP发送完成
  while (I2CMSST & 0x10)
    ; // 等待总线空闲
  P_SW2 &= ~0x80;
}

// 发送一个字节
void HW_I2C_SendByte(uint8_t byte) {
  P_SW2 |= 0x80;
  // 写入数据到发送寄存器
  I2CTXD = byte;
  // 启动发送
  I2CMSCR = 0x02; // WR=1
  // 等待发送完成
  while (!(I2CMSST & 0x08))
    ;               // 等待TBE=1
  I2CMSST &= ~0x08; // 清除TBE标志
  P_SW2 &= ~0x80;
}

// 接收一个字节
uint8_t HW_I2C_ReceiveByte(void) {
  uint8_t dat;
  P_SW2 |= 0x80;
  // 启动接收
  I2CMSCR = 0x02; // WR=1（读操作）
  // 等待接收完成
  while (!(I2CMSST & 0x40))
    ; // 等待RBNE=1
  dat = I2CRXD;
  I2CMSST &= ~0x40; // 清除RBNE标志
  P_SW2 &= ~0x80;
  return dat;
}

// 等待应答
bit HW_I2C_WaitAck(void) {
  P_SW2 |= 0x80;
  // 等待ACK/NACK
  while (!(I2CMSST & 0x04))
    ; // 等待ADRS=1
  // 检查是否收到ACK
  if (I2CMSST & 0x80) {
    // NOACK=1，未收到ACK
    I2CMSST &= ~0x04; // 清除ADRS标志
    P_SW2 &= ~0x80;
    return 1;
  }
  I2CMSST &= ~0x04; // 清除ADRS标志
  P_SW2 &= ~0x80;
  return 0;
}

// 发送应答
void HW_I2C_SendAck(bit ack) {
  P_SW2 |= 0x80;
  if (ack) {
    // 发送NACK
    I2CMSCR = 0x12; // NACK=1, WR=1
  } else {
    // 发送ACK
    I2CMSCR = 0x02; // WR=1
  }
  // 等待发送完成
  while (!(I2CMSST & 0x08))
    ;               // 等待TBE=1
  I2CMSST &= ~0x08; // 清除TBE标志
  P_SW2 &= ~0x80;
}

// 向EEPROM写入一个字节
void HW_EEPROM_WriteByte(uint16_t addr, uint8_t dat) {
  HW_I2C_Start();
  HW_I2C_SendByte(EEPROM_ADDR);
  if (HW_I2C_WaitAck()) {
    HW_I2C_Stop();
    return;
  }
  HW_I2C_SendByte(addr >> 8);
  if (HW_I2C_WaitAck()) {
    HW_I2C_Stop();
    return;
  }
  HW_I2C_SendByte(addr & 0xFF);
  if (HW_I2C_WaitAck()) {
    HW_I2C_Stop();
    return;
  }
  HW_I2C_SendByte(dat);
  if (HW_I2C_WaitAck()) {
    HW_I2C_Stop();
    return;
  }
  HW_I2C_Stop();
  // 等待写入完成
  uint8_t wait_i = 255;
  while (wait_i--)
    ;
}

// 从EEPROM读取一个字节
uint8_t HW_EEPROM_ReadByte(uint16_t addr) {
  uint8_t dat;
  HW_I2C_Start();
  HW_I2C_SendByte(EEPROM_ADDR);
  if (HW_I2C_WaitAck()) {
    HW_I2C_Stop();
    return 0;
  }
  HW_I2C_SendByte(addr >> 8);
  if (HW_I2C_WaitAck()) {
    HW_I2C_Stop();
    return 0;
  }
  HW_I2C_SendByte(addr & 0xFF);
  if (HW_I2C_WaitAck()) {
    HW_I2C_Stop();
    return 0;
  }
  HW_I2C_Start();
  HW_I2C_SendByte(EEPROM_ADDR | 0x01);
  if (HW_I2C_WaitAck()) {
    HW_I2C_Stop();
    return 0;
  }
  dat = HW_I2C_ReceiveByte();
  HW_I2C_SendAck(1);
  HW_I2C_Stop();
  return dat;
}

// 向EEPROM写入字符串
void HW_EEPROM_WriteString(uint16_t addr, uint8_t *str) {
  while (*str) {
    HW_EEPROM_WriteByte(addr++, *str++);
  }
  HW_EEPROM_WriteByte(addr, 0); // 写入结束符
}

// 从EEPROM读取字符串
void HW_EEPROM_ReadString(uint16_t addr, uint8_t *str, uint16_t len) {
  uint16_t read_str_i;
  for (read_str_i = 0; read_str_i < len; read_str_i++) {
    *str = HW_EEPROM_ReadByte(addr++);
    if (*str == 0)
      break;
    str++;
  }
  *str = 0; // 确保字符串结束
}

// 读取EEPROM指定长度内容
void HW_EEPROM_ReadBuffer(uint16_t addr, uint8_t *buffer, uint16_t len) {
  uint16_t read_i;
  for (read_i = 0; read_i < len; read_i++) {
    buffer[read_i] = HW_EEPROM_ReadByte(addr + read_i);
  }
}

// 清空EEPROM全部内容
void HW_EEPROM_ClearAll(void) {
  uint16_t clear_i;
  for (clear_i = 0; clear_i < 8192; clear_i++) { // 24C64容量为8KB
    HW_EEPROM_WriteByte(clear_i, 0);
  }
}

// 设置设备地址
void HW_EEPROM_SetAddress(uint16_t addr) { EEPROM_ADDR = addr; }