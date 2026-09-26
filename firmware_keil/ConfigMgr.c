#include "ConfigMgr.h"
#include "StorageMgr.h"
#include "FeatTag.h"
#include <string.h>

// 读 16 位小端
static uint16_t ConfigMgr_ReadU16(uint16_t addr) {
  return (uint16_t)EEPROM_ReadByte(addr) |
         ((uint16_t)EEPROM_ReadByte(addr + 1) << 8);
}

// 写 16 位小端
static void ConfigMgr_WriteU16(uint16_t addr, uint16_t val) {
  EEPROM_WriteByte(addr, (uint8_t)(val & 0xFF));
  EEPROM_WriteByte(addr + 1, (uint8_t)(val >> 8));
}

// 填充默认配置
void ConfigMgr_Defaults(cfg_t *cfg) {
  memset(cfg, 0, sizeof(cfg_t));
  cfg->flags = FLAG_INITIALIZED;
  cfg->encrypt = ENC_ALGO_NONE;
  cfg->key_vol = KEY_VOL_DEFAULT;
  cfg->dev_size = DEV_SIZE_DEFAULT;
}

// 读取整个配置区到缓存
void ConfigMgr_Load(cfg_t *cfg) {
  uint8_t i;

  EEPROM_SetAddress(IC_0_24C64);
  cfg->flags   = EEPROM_ReadByte(CFG_ADDR_FLAGS);
  cfg->encrypt = EEPROM_ReadByte(CFG_ADDR_ENC_ALGO);
  cfg->key_vol = EEPROM_ReadByte(CFG_ADDR_KEY_VOL);
  cfg->dev_size = EEPROM_ReadByte(CFG_ADDR_DEV_SIZE);
  for (i = 0; i < 16; i++) cfg->pwd_hash[i] = EEPROM_ReadByte(CFG_ADDR_PWD_HASH + i);
  for (i = 0; i < CFG_NAME_LEN; i++) cfg->name[i] = (char)EEPROM_ReadByte(CFG_ADDR_NAME + i);
  cfg->name[CFG_NAME_LEN - 1] = '\0';
  cfg->access_count = ConfigMgr_ReadU16(CFG_ADDR_ACCESS_CNT);
}

// 计算配置区校验和(逐字段异或)
uint8_t ConfigMgr_Checksum(const cfg_t *cfg) {
  uint8_t i, sum = 0;

  sum ^= cfg->flags ^ cfg->encrypt ^ cfg->key_vol ^ cfg->dev_size;
  for (i = 0; i < 16; i++) sum ^= cfg->pwd_hash[i];
  for (i = 0; i < CFG_NAME_LEN; i++) sum ^= (uint8_t)cfg->name[i];
  sum ^= (uint8_t)(cfg->access_count & 0xFF) ^ (uint8_t)(cfg->access_count >> 8);
  return sum;
}

// 回写整个配置区并重算校验和
void ConfigMgr_Save(const cfg_t *cfg) {
  uint8_t i;

  EEPROM_SetAddress(IC_0_24C64);
  EEPROM_WriteByte(CFG_ADDR_FLAGS, cfg->flags);
  EEPROM_WriteByte(CFG_ADDR_ENC_ALGO, cfg->encrypt);
  EEPROM_WriteByte(CFG_ADDR_KEY_VOL, cfg->key_vol);
  EEPROM_WriteByte(CFG_ADDR_DEV_SIZE, cfg->dev_size);
  for (i = 0; i < 16; i++) EEPROM_WriteByte(CFG_ADDR_PWD_HASH + i, cfg->pwd_hash[i]);
  for (i = 0; i < CFG_NAME_LEN; i++) EEPROM_WriteByte(CFG_ADDR_NAME + i, (uint8_t)cfg->name[i]);
  ConfigMgr_WriteU16(CFG_ADDR_ACCESS_CNT, cfg->access_count);
  EEPROM_WriteByte(CFG_ADDR_CHECKSUM, ConfigMgr_Checksum(cfg));
}

// 写设备名(截断补0)
void ConfigMgr_SetName(cfg_t *cfg, const char *name) {
  uint8_t i, len = (uint8_t)strlen(name);

  if (len > CFG_NAME_LEN - 1) len = CFG_NAME_LEN - 1;
  for (i = 0; i < CFG_NAME_LEN; i++) cfg->name[i] = (i < len) ? name[i] : 0x00;
}

// 写密码摘要(UPASS=空)
void ConfigMgr_SetPassword(cfg_t *cfg, const char *pwd) {
  memset(cfg->pwd_hash, 0, 16);
  if (pwd == NULL || strlen(pwd) == 0 || strcmp(pwd, PASS_NONE_STR) == 0) {
    cfg->flags &= (uint8_t)~FLAG_PWD_AUTH;
    return;
  }
  FeatTag_Checksum(pwd, (uint8_t)strlen(pwd), cfg->pwd_hash);
  cfg->flags |= FLAG_PWD_AUTH;
}

// 读状态标志位
uint8_t ConfigMgr_GetFlags(void) {
  EEPROM_SetAddress(IC_0_24C64);
  return EEPROM_ReadByte(CFG_ADDR_FLAGS);
}

// 写状态标志位
void ConfigMgr_SetFlags(uint8_t flags) {
  EEPROM_SetAddress(IC_0_24C64);
  EEPROM_WriteByte(CFG_ADDR_FLAGS, flags);
}

// 读接入计数
uint16_t ConfigMgr_GetAccessCount(void) {
  EEPROM_SetAddress(IC_0_24C64);
  return ConfigMgr_ReadU16(CFG_ADDR_ACCESS_CNT);
}

// 接入计数+1, 65535封顶
void ConfigMgr_AddAccessCount(void) {
  uint16_t count = ConfigMgr_GetAccessCount();

  if (count < 0xFFFF) count++;
  ConfigMgr_WriteU16(CFG_ADDR_ACCESS_CNT, count);
}
