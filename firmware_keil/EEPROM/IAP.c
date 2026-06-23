#include "IAP.h"

// STC8G1K08A EEPROM相关常量
#define EEPROM_START_ADDR 0x0000 // EEPROM起始地址
#define EEPROM_END_ADDR 0x03FF   // EEPROM结束地址(1KB)
#define EEPROM_SIZE 1024         // EEPROM大小(字节)

/**
 * @brief 使能IAP功能
 * @param sys_clk_mhz 系统时钟频率(MHz)，用于设置正确的等待时间
 */
void IAP_Enable(uint8_t sys_clk_mhz) {
  IAP_CONTR = IAP_EN;                        // 使能IAP功能
  IAP_TPS = (sys_clk_mhz * 1000000) / 10000; // 设置IAP操作等待时间
}

/**
 * @brief 禁用IAP功能
 */
void IAP_Disable(void) {
  IAP_CONTR = 0x00; // 禁用IAP
  IAP_CMD = 0x00;   // 清除命令
}

/**
 * @brief 读取EEPROM指定地址的一个字节
 * @param addr EEPROM地址(0x0000-0x03FF)
 * @return 读取到的字节数据
 */
uint8_t EEPROM_ReadByte(uint16_t addr) {
  uint8_t data;

  if (addr > EEPROM_END_ADDR)
    return 0xFF; // 地址越界返回0xFF

  IAP_Enable(11); // 假设系统时钟为11MHz

  IAP_ADDRH = (uint8_t)(addr >> 8);
  IAP_ADDRL = (uint8_t)addr;
  IAP_CMD = IAP_CMD_READ;
  IAP_TRIG = 0x5A; // 触发IAP操作
  IAP_TRIG = 0xA5;

  data = IAP_DATA;

  IAP_Disable();

  return data;
}

/**
 * @brief 读取EEPROM指定地址的某一位
 * @param addr EEPROM地址(0x0000-0x03FF)
 * @param bit_pos 位位置(0-7)，0为最低位
 * @return 读取到的位值(0或1)
 */
bit EEPROM_ReadBit(uint16_t addr, uint8_t bit_pos) {
  uint8_t byte_data;

  if (bit_pos > 7)
    return 0;

  byte_data = EEPROM_ReadByte(addr);

  return (byte_data >> bit_pos) & 0x01;
}

/**
 * @brief 读取EEPROM指定地址开始的多个字节
 * @param addr 起始地址
 * @param len 读取长度
 * @param buf 存储数据的缓冲区指针
 * @return 实际读取的字节数
 */
uint16_t EEPROM_ReadBytes(uint16_t addr, uint16_t len, uint8_t *buf) {
  uint16_t i;
  uint16_t read_len = 0;

  if (buf == NULL || len == 0)
    return 0;

  // 计算实际可读取的长度
  if (addr + len > EEPROM_END_ADDR + 1) {
    read_len = EEPROM_END_ADDR - addr + 1;
  } else {
    read_len = len;
  }

  for (i = 0; i < read_len; i++) {
    buf[i] = EEPROM_ReadByte(addr + i);
  }

  return read_len;
}

/**
 * @brief 读取EEPROM指定地址范围的内容
 * @param start_addr 起始地址
 * @param end_addr 结束地址(包含)
 * @param buf 存储数据的缓冲区指针
 * @return 实际读取的字节数
 */
uint16_t EEPROM_ReadRange(uint16_t start_addr, uint16_t end_addr,
                          uint8_t *buf) {
  if (start_addr > end_addr || buf == NULL)
    return 0;

  // 修正地址范围
  if (start_addr > EEPROM_END_ADDR)
    return 0;
  if (end_addr > EEPROM_END_ADDR)
    end_addr = EEPROM_END_ADDR;

  return EEPROM_ReadBytes(start_addr, end_addr - start_addr + 1, buf);
}

/**
 * @brief 读取整个EEPROM内容
 * @param buf 存储数据的缓冲区指针(至少1024字节)
 * @return 实际读取的字节数
 */
uint16_t EEPROM_ReadAll(uint8_t *buf) {
  if (buf == NULL)
    return 0;

  return EEPROM_ReadBytes(EEPROM_START_ADDR, EEPROM_SIZE, buf);
}

/**
 * @brief 向EEPROM指定地址写入一个字节
 * @param addr EEPROM地址(0x0000-0x03FF)
 * @param data 要写入的字节数据
 */
void EEPROM_WriteByte(uint16_t addr, uint8_t data) {
  if (addr > EEPROM_END_ADDR)
    return;

  IAP_Enable(11); // 假设系统时钟为11MHz

  IAP_ADDRH = (uint8_t)(addr >> 8);
  IAP_ADDRL = (uint8_t)addr;
  IAP_DATA = data;
  IAP_CMD = IAP_CMD_PROG;
  IAP_TRIG = 0x5A; // 触发IAP操作
  IAP_TRIG = 0xA5;

  IAP_Disable();
}

/**
 * @brief 向EEPROM指定地址写入多个字节
 * @param addr 起始地址
 * @param len 写入长度
 * @param buf 要写入的数据缓冲区指针
 * @return 实际写入的字节数
 */
uint16_t EEPROM_WriteBytes(uint16_t addr, uint16_t len, const uint8_t *buf) {
  uint16_t i;
  uint16_t write_len = 0;

  if (buf == NULL || len == 0)
    return 0;

  // 计算实际可写入的长度
  if (addr + len > EEPROM_END_ADDR + 1) {
    write_len = EEPROM_END_ADDR - addr + 1;
  } else {
    write_len = len;
  }

  for (i = 0; i < write_len; i++) {
    EEPROM_WriteByte(addr + i, buf[i]);
  }

  return write_len;
}

/**
 * @brief 擦除指定扇区(STC8G1K08A扇区大小为512字节)
 * @param sector_addr 扇区起始地址(0x0000或0x0200)
 */
void EEPROM_EraseSector(uint16_t sector_addr) {
  // STC8G1K08A的EEPROM分为2个扇区: 0x0000-0x01FF 和 0x0200-0x03FF
  if (sector_addr != 0x0000 && sector_addr != 0x0200)
    return;

  IAP_Enable(11); // 假设系统时钟为11MHz

  IAP_ADDRH = (uint8_t)(sector_addr >> 8);
  IAP_ADDRL = (uint8_t)sector_addr;
  IAP_CMD = IAP_CMD_ERASE;
  IAP_TRIG = 0x5A; // 触发IAP操作
  IAP_TRIG = 0xA5;

  IAP_Disable();
}

/**
 * @brief 擦除整个EEPROM(写入0xFF)
 */
void EEPROM_EraseAll(void) {
  // 擦除两个扇区
  EEPROM_EraseSector(0x0000);
  EEPROM_EraseSector(0x0200);
}

/**
 * @brief 将EEPROM指定地址范围填充为指定值
 * @param addr 起始地址
 * @param len 填充长度
 * @param value 填充值
 * @return 实际填充的字节数
 */
uint16_t EEPROM_Fill(uint16_t addr, uint16_t len, uint8_t value) {
  uint16_t i;
  uint16_t fill_len = 0;

  if (len == 0)
    return 0;

  // 计算实际可填充的长度
  if (addr + len > EEPROM_END_ADDR + 1) {
    fill_len = EEPROM_END_ADDR - addr + 1;
  } else {
    fill_len = len;
  }

  for (i = 0; i < fill_len; i++) {
    EEPROM_WriteByte(addr + i, value);
  }

  return fill_len;
}

/**
 * @brief 将EEPROM指定地址范围清空为0x00
 * @param addr 起始地址
 * @param len 清空长度
 * @return 实际清空的字节数
 */
uint16_t EEPROM_Clear(uint16_t addr, uint16_t len) {
  return EEPROM_Fill(addr, len, 0x00);
}

/**
 * @brief 清空整个EEPROM为0x00
 */
void EEPROM_ClearAll(void) {
  EEPROM_Fill(EEPROM_START_ADDR, EEPROM_SIZE, 0x00);
}

/**
 * @brief 检查EEPROM指定地址范围是否全为指定值
 * @param addr 起始地址
 * @param len 检查长度
 * @param value 要检查的值
 * @return 1: 全部相等, 0: 存在不相等
 */
bit EEPROM_CheckValue(uint16_t addr, uint16_t len, uint8_t value) {
  uint16_t i;

  if (addr + len > EEPROM_END_ADDR + 1)
    len = EEPROM_END_ADDR - addr + 1;

  for (i = 0; i < len; i++) {
    if (EEPROM_ReadByte(addr + i) != value)
      return 0;
  }

  return 1;
}

/**
 * @brief 比较EEPROM指定地址内容与缓冲区数据
 * @param addr EEPROM起始地址
 * @param len 比较长度
 * @param buf 比较数据缓冲区
 * @return 1: 相等, 0: 不相等
 */
bit EEPROM_Compare(uint16_t addr, uint16_t len, const uint8_t *buf) {
  uint16_t i;

  if (buf == NULL || len == 0)
    return 0;

  if (addr + len > EEPROM_END_ADDR + 1)
    len = EEPROM_END_ADDR - addr + 1;

  for (i = 0; i < len; i++) {
    if (EEPROM_ReadByte(addr + i) != buf[i])
      return 0;
  }

  return 1;
}

/**
 * @brief 向EEPROM指定地址写入一个位
 * @param addr EEPROM地址(0x0000-0x03FF)
 * @param bit_pos 位位置(0-7)
 * @param bit_value 位值(0或1)
 */
void EEPROM_WriteBit(uint16_t addr, uint8_t bit_pos, bit bit_value) {
  uint8_t byte_data;

  if (bit_pos > 7 || addr > EEPROM_END_ADDR)
    return;

  byte_data = EEPROM_ReadByte(addr);

  if (bit_value) {
    byte_data |= (1 << bit_pos);
  } else {
    byte_data &= ~(1 << bit_pos);
  }

  EEPROM_WriteByte(addr, byte_data);
}

/**
 * @brief 切换EEPROM指定地址的某一位
 * @param addr EEPROM地址(0x0000-0x03FF)
 * @param bit_pos 位位置(0-7)
 */
void EEPROM_ToggleBit(uint16_t addr, uint8_t bit_pos) {
  uint8_t byte_data;

  if (bit_pos > 7 || addr > EEPROM_END_ADDR)
    return;

  byte_data = EEPROM_ReadByte(addr);
  byte_data ^= (1 << bit_pos);
  EEPROM_WriteByte(addr, byte_data);
}