#ifndef __CONFIG_MGR_H__
#define __CONFIG_MGR_H__

#include "HW_24c64.h"

/* 设备配置区(第一块配置区 0x00~0xFF)读写业务层
 * 统一管理状态标志、密码摘要、设备名、加密算法、容量、接入计数等字段,
 * 多字节字段一律小端, 与 StorageMgr 布局保持一致。 */

/* 配置区缓存结构 */
typedef struct {
  uint8_t  flags;          /* 状态标志位 */
  uint8_t  encrypt;        /* 加密算法 ENC_ALGO_* */
  uint8_t  key_vol;        /* 每块键值对数量 */
  uint8_t  dev_size;       /* 设备大小 (KB) */
  uint8_t  pwd_hash[16];   /* 密码摘要(FeatTag) */
  char     name[18];       /* 设备名称 */
  uint16_t access_count;   /* 接入计数 */
} cfg_t;

void     ConfigMgr_Load(cfg_t *cfg);                       /* 读取整个配置区到缓存 */
void     ConfigMgr_Save(const cfg_t *cfg);                 /* 回写整个配置区并重算校验和 */
void     ConfigMgr_Defaults(cfg_t *cfg);                   /* 填充默认配置 */

void     ConfigMgr_SetName(cfg_t *cfg, const char *name);  /* 写设备名(截断补0) */
void     ConfigMgr_SetPassword(cfg_t *cfg, const char *pwd);/* 写密码摘要(UPASS=空) */

uint8_t  ConfigMgr_GetFlags(void);                         /* 读状态标志位 */
void     ConfigMgr_SetFlags(uint8_t flags);                /* 写状态标志位 */
uint16_t ConfigMgr_GetAccessCount(void);                   /* 读接入计数 */
void     ConfigMgr_AddAccessCount(void);                   /* 接入计数+1, 65535封顶 */
uint8_t  ConfigMgr_Checksum(const cfg_t *cfg);             /* 计算配置区校验和 */

#endif /* __CONFIG_MGR_H__ */
