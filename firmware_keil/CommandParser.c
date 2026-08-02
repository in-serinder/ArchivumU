#include "CommandParser.h"
#include "EEPROM/HW_24c64.h"
#include "StorageMgr.h"
#include "BLAKE2s.h"
#include <string.h>

static uint16_t s_storage_size = 0;   /* 存储区大小(由CMD_PARSER_SET_STORAGE设置)，与HW层的EEPROM_ADDR(I2C从地址)区分 */

/* 固件信息(INFO返回, 与上位机解析格式保持一致) */
#define FIRMWARE_VERSION "V1.0.0"

// 字符串分割辅助函数
static int CMD_Split(char *str, char delim, char **tokens, int max_tokens) {
  int count = 0;
  char delimiters[2];
  char *token;
  delimiters[0] = delim;
  delimiters[1] = '\0';
  token = strtok(str, delimiters);
  while (token != NULL && count < max_tokens) {
    tokens[count++] = token;
    token = strtok(NULL, delimiters);
  }
  return count;
}

// 字符串比较辅助函数
static int CMD_Compare(const char *a, const char *b) {
  return strcmp(a, b) == 0;
}

// 字符串转十进制数(用于解析加密方式等数字参数)
static uint8_t CMD_Atoi(char *str) {
  uint8_t val = 0;
  while (*str >= '0' && *str <= '9') {
    val = (uint8_t)(val * 10 + (uint8_t)(*str - '0'));
    str++;
  }
  return val;
}

// 主入口：解析串口传入的AT指令字符串并导航到对应函数
void CMD_Parser(char *cmd) {
  char *tokens[10];
  int token_count = CMD_Split(cmd, '+', tokens, 10);

  if (token_count < 1)
    return;

  // 检查是否为AT指令
  if (!CMD_Compare(tokens[0], "AT"))
    return;

  if (token_count < 2)
    return;

  // 解析二级指令
    if (CMD_Compare(tokens[1], "INIT") && token_count >= 5) {
    CMD_INIT(tokens[2], tokens[3], CMD_Atoi(tokens[4]));
  } else if (CMD_Compare(tokens[1], "ECHO")) {
    CMD_ECHO();
  } else if (CMD_Compare(tokens[1], "INFO")) {
    CMD_INFO();
  } else if (CMD_Compare(tokens[1], "STATUS")) {
    CMD_STATUS();
  } else if (CMD_Compare(tokens[1], "AUTH") && token_count >= 4) {
    if (CMD_Compare(tokens[2], "PASSWORD")) {
      if (CMD_Compare(tokens[3], "CREATE") && token_count >= 5) {
        CMD_AUTH_CREATE(tokens[4]);
      } else if (CMD_Compare(tokens[3], "VERIFY") && token_count >= 5) {
        CMD_AUTH_VERIFY(tokens[4]);
      } else if (CMD_Compare(tokens[3], "ENABLE")) {
        CMD_AUTH_ENABLE();
      } else if (CMD_Compare(tokens[3], "DISABLE")) {
        CMD_AUTH_DISABLE();
      } else if (CMD_Compare(tokens[3], "VERIFYOUT")) {
        CMD_AUTH_VERIFYOUT();
      }
    }
  } else if (CMD_Compare(tokens[1], "READ")) {
    if (token_count >= 3) {
      if (CMD_Compare(tokens[2], "BLOCK") && token_count >= 4) {
        CMD_READ_BLOCK(tokens[3]);
      } else if (CMD_Compare(tokens[2], "KEY") && token_count >= 5) {
        CMD_READ_KEY(tokens[3], tokens[4]);
      } else {
        CMD_READ(tokens[2]);
      }
    }
  } else if (CMD_Compare(tokens[1], "WRITE") && token_count >= 5) {
    CMD_WRITE(tokens[2], tokens[3], tokens[4]);
  } else if (CMD_Compare(tokens[1], "CREATE")) {
    if (CMD_Compare(tokens[2], "BLOCK") && token_count >= 5) {
      CMD_CREATE_BLOCK(tokens[3], tokens[4]);
    } else if (CMD_Compare(tokens[2], "KEY") && token_count >= 6) {
      CMD_CREATE_KEY(tokens[3], tokens[4], tokens[5], tokens[6]);
    }
  } else if (CMD_Compare(tokens[1], "DELETE")) {
    if (CMD_Compare(tokens[2], "BLOCK") && token_count >= 4) {
      CMD_DELETE_BLOCK(tokens[3]);
    } else if (CMD_Compare(tokens[2], "KEY") && token_count >= 6) {
      CMD_DELETE_KEY(tokens[3], tokens[4], tokens[5], tokens[6]);
    }
  } else if (CMD_Compare(tokens[1], "UPDATE")) {
    if (CMD_Compare(tokens[2], "BLOCK") && token_count >= 4) {
      CMD_UPDATE_BLOCK(tokens[3]);
    } else if (CMD_Compare(tokens[2], "KEY") && token_count >= 5) {
      CMD_UPDATE_KEY(tokens[3], tokens[4], tokens[5]);
    }
  } else if (CMD_Compare(tokens[1], "GET") && CMD_Compare(tokens[2], "ALL") &&
             CMD_Compare(tokens[3], "BLOCK")) {
    CMD_GET_ALL_BLOCK();
  } else if (CMD_Compare(tokens[1], "FORMAT")) {
    if (CMD_Compare(tokens[2], "DEV")) {
      CMD_FORMAT_DEV();
    } else if (CMD_Compare(tokens[2], "BLOCK") && token_count >= 5) {
      CMD_FORMAT_BLOCK(tokens[3], tokens[4]);
    }
  }
}


void CMD_PARSER_SET_STORAGE(uint16_t addr) {
    s_storage_size = addr;
}


/*起始指令类*/
// CMD_INIT: AT+INIT+<设备名>+<密码(UPASS表示不启用密码)>+<加密方式(0不加密/1AES/2XOR/3凯撒/4RC4)>
void CMD_INIT(char *device_name, char *password, uint8_t encrypt_type) {
  // 导航路径: 起始 -> 设备初始化
  uint8_t i;
  uint8_t ch;
  uint8_t name_len;
  uint8_t hash[16];                    /* BLAKE2s输出16字节 */
  uint8_t flags = FLAG_INITIALIZED;    /* bit0: 设备已初始化 */
  uint8_t checksum = 0;
  bit enablePassword;                  /* 密码非空且非"UPASS"才启用 */

  enablePassword = (strlen(password) > 0) && (!CMD_Compare(password, PASS_NONE_STR));

  if (enablePassword) flags |= FLAG_PWD_AUTH;              /* bit3: 密码鉴权功能启用 */
  if (encrypt_type != ENC_NONE) flags |= FLAG_FILE_ENCRYPT; /* bit4: 文件加密功能启用 */

  // 对EEPROM全部写入0x00 初始化状态标志 (整片8KB清零)
  EEPROM_SetAddress(IC_0_24C64);
  DM_format_block(DEV_EEPROM_SIZE, 0x00);

  // 0x01: 状态标志位
  EEPROM_WriteByte(CFG_ADDR_FLAGS, flags);
  checksum ^= flags;

  // 0x02~0x11: 密码哈希(BLAKE2s输出16字节, 0x12~0x21 保持0x00)
  if (enablePassword) {
    BLAKE2s_Checksum(password, (uint8_t)strlen(password), hash);
    for (i = 0; i < 16; i++) {
      EEPROM_WriteByte(CFG_ADDR_PWD_HASH + i, hash[i]);
      checksum ^= hash[i];
    }
  }

  // 0x3C~0x4D: 设备名称(18字节, 不足补0x00)
  name_len = (uint8_t)strlen(device_name);
  if (name_len > CFG_NAME_LEN) name_len = CFG_NAME_LEN;
  for (i = 0; i < CFG_NAME_LEN; i++) {
    ch = (i < name_len) ? (uint8_t)device_name[i] : 0x00;
    EEPROM_WriteByte(CFG_ADDR_NAME + i, ch);
    checksum ^= ch;
  }

  // 0x4E: 加密方式
  EEPROM_WriteByte(CFG_ADDR_ENCRYPT, encrypt_type);
  checksum ^= encrypt_type;

  // 0x4F: 键值对数量, 0x50: 设备大小
  EEPROM_WriteByte(CFG_ADDR_KEY_VOL, KEY_VOL_DEFAULT);
  EEPROM_WriteByte(CFG_ADDR_DEV_SIZE, DEV_SIZE_DEFAULT);
  checksum ^= KEY_VOL_DEFAULT ^ DEV_SIZE_DEFAULT;

  // 0x00: 整片配置区校验和 (全区0x00~0xFF异或为0)
  EEPROM_WriteByte(CFG_ADDR_CHECKSUM, checksum);
}

// CMD_ECHO: AT+ECHO
void CMD_ECHO(void) {
  // 导航路径: 起始 -> 设备检测
}

// CMD_INFO: AT+INFO
// 已初始化: INFO+<设备名>+<固件版本>+<密码状态>+<接入计数>+<块数量>+<键值对数量>+<加密方式>+<存储总大小>
// 未初始化: INIT=0+<固件版本>
void CMD_INFO(void) {
  // 导航路径: 起始 -> 获取设备信息
  uint8_t i;
  uint8_t flags;
  uint8_t enc_type;
  uint8_t key_vol;
  uint8_t block_cnt;
  uint8_t dev_size;
  char name[CFG_NAME_LEN + 1];

  flags = EEPROM_ReadByte(CFG_ADDR_FLAGS);

  // 设备未初始化 -> INIT=0+<固件版本>
  if (!(flags & FLAG_INITIALIZED)) {
    Uart1_SendString("INIT=0+");
    Uart1_SendString(FIRMWARE_VERSION);
    Uart1_SendString("\r\n");
    return;
  }

  // 读取设备名(18字节, 末尾补0保证字符串结束)
  for (i = 0; i < CFG_NAME_LEN; i++) {
    name[i] = (char)EEPROM_ReadByte(CFG_ADDR_NAME + i);
  }
  name[CFG_NAME_LEN] = '\0';

  enc_type  = EEPROM_ReadByte(CFG_ADDR_ENCRYPT);   /* 加密方式 */
  key_vol   = EEPROM_ReadByte(CFG_ADDR_KEY_VOL);   /* 键值对数量 */
  block_cnt = EEPROM_ReadByte(CFG_ADDR_BLOCK_CNT); /* 块数量 */
  dev_size  = EEPROM_ReadByte(CFG_ADDR_DEV_SIZE);  /* 设备大小 */

  Uart1_SendString("INFO+");
  Uart1_SendString(name);
  Uart1_SendString("+");
  Uart1_SendString(FIRMWARE_VERSION);
  Uart1_SendString("+");

  // 密码状态
  if (flags & FLAG_PWD_AUTH) {
    Uart1_SendString("ENABLED");
  } else {
    Uart1_SendString("DISABLED");
  }
  Uart1_SendString("+");

  Uart1_SendNumber(DM_UPDATECOUNT_Get());  /* 接入计数 */
  Uart1_SendString("+");
  Uart1_SendNumber(block_cnt);             /* 块数量 */
  Uart1_SendString("+");
  Uart1_SendNumber(key_vol);               /* 键值对数量 */
  Uart1_SendString("+");

  // 加密方式 0-NON 1-AES 2-XOR 3-CESAR 4-RC4
  switch (enc_type) {
    case ENC_AES:    Uart1_SendString("AES");   break;
    case ENC_XOR:    Uart1_SendString("XOR");   break;
    case ENC_CAESAR: Uart1_SendString("CESAR"); break;
    case ENC_RC4:    Uart1_SendString("RC4");   break;
    default:         Uart1_SendString("NON");   break;
  }
  Uart1_SendString("+");

  // 存储总大小: 两片24C64合计 = DEV_SIZE*1024*2 (默认8*1024*2=16384字节)
  Uart1_SendNumber((uint16_t)((uint16_t)dev_size * 2048));
  Uart1_SendString("\r\n");
}

// CMD_STATUS: AT+STATUS
void CMD_STATUS(void) {
  // 导航路径: 起始 -> 获取设备状态
}

/*身份验证指令类*/
// CMD_AUTH_CREATE: AT+AUTH+PASSWORD+CREATE+<密码>
void CMD_AUTH_CREATE(char *password) {
  // 导航路径: 身份验证 -> 创建密码
}

// CMD_AUTH_VERIFY: AT+AUTH+PASSWORD+VERIFY+<密码>
void CMD_AUTH_VERIFY(char *password) {
  // 导航路径: 身份验证 -> 验证密码
}

// CMD_AUTH_ENABLE: AT+AUTH+PASSWORD+ENABLE
void CMD_AUTH_ENABLE(void) {
  // 导航路径: 身份验证 -> 启用密码验证
}

// CMD_AUTH_DISABLE: AT+AUTH+PASSWORD+DISABLE
void CMD_AUTH_DISABLE(void) {
  // 导航路径: 身份验证 -> 禁用密码验证
}

// CMD_AUTH_VERIFYOUT: AT+AUTH+PASSWORD+VERIFYOUT
void CMD_AUTH_VERIFYOUT(void) {
  // 导航路径: 身份验证 -> 退出验证状态
}

/*功能性CURD指令类*/

/*读取指令*/
// CMD_READ: AT+READ+<读取单位>
void CMD_READ(char *unit) {
  // 导航路径: CURD -> 读取
}

// CMD_READ_BLOCK: AT+READ+BLOCK+<块ID>
void CMD_READ_BLOCK(char *block_id) {
  // 导航路径: CURD -> 读取 -> 读取块
}

// CMD_READ_KEY: AT+READ+KEY+<BLOCKID>+<KEY>
void CMD_READ_KEY(char *block_id, char *key) {
  // 导航路径: CURD -> 读取 -> 读取键值
}

/*写入指令*/
// CMD_WRITE: AT+WRITE+<块ID>+<KEY>+<KEY_VALUE>
void CMD_WRITE(char *block_id, char *key, char *key_value) {
  // 导航路径: CURD -> 写入
}

/*创建指令*/
// CMD_CREATE_BLOCK: AT+CREATE+BLOCK+<块名>+<块大小>
void CMD_CREATE_BLOCK(char *block_name, char *block_size) {
  // 导航路径: CURD -> 创建 -> 创建块
}

// CMD_CREATE_KEY: AT+CREATE+KEY+<块标识>+<块ID或块名>+<键>+<值>
void CMD_CREATE_KEY(char *block_flag, char *block_identifier, char *key,
                    char *value) {
  // 导航路径: CURD -> 创建 -> 创建键值
}

/*删除指令*/
// CMD_DELETE_BLOCK: AT+DELETE+BLOCK+<块ID>
void CMD_DELETE_BLOCK(char *block_id) {
  // 导航路径: CURD -> 删除 -> 删除块
}

// CMD_DELETE_KEY: AT+DELETE+KEY+<块标识>+<块ID或块名>+<键>+<值>
void CMD_DELETE_KEY(char *block_flag, char *block_identifier, char *key,
                    char *value) {
  // 导航路径: CURD -> 删除 -> 删除键值
}

/*更新指令*/
// CMD_UPDATE_BLOCK: AT+UPDATE+BLOCK+<块ID>
void CMD_UPDATE_BLOCK(char *block_id) {
  // 导航路径: CURD -> 更新 -> 更新块
}

// CMD_UPDATE_KEY: AT+UPDATE+KEY+<BLOCKID>+<KEY>+<KEY_VALUE>
void CMD_UPDATE_KEY(char *block_id, char *key, char *key_value) {
  // 导航路径: CURD -> 更新 -> 更新键值
}

/*全获取指令类*/
// CMD_GET_ALL_BLOCK: AT+GET+ALL+BLOCK
void CMD_GET_ALL_BLOCK(void) {
  // 导航路径: 全获取 -> 获取所有块
}

/*格式化指令类*/
// CMD_FORMAT_DEV: AT+FORMAT+DEV
void CMD_FORMAT_DEV(void) {
  // 导航路径: 格式化 -> 格式化设备
}

// CMD_FORMAT_BLOCK: AT+FORMAT+BLOCK+<块标识>+<块ID或块名>
void CMD_FORMAT_BLOCK(char *block_flag, char *block_identifier) {
  // 导航路径: 格式化 -> 格式化块
}