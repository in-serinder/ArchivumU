#include "CommandParser.h"
#include <string.h>

// 字符串分割辅助函数
static int CMD_Split(char *str, char delim, char **tokens, int max_tokens) {
  int count = 0;
  char *token = strtok(str, &delim);
  while (token != NULL && count < max_tokens) {
    tokens[count++] = token;
    token = strtok(NULL, &delim);
  }
  return count;
}

// 字符串比较辅助函数
static int CMD_Compare(const char *a, const char *b) {
  return strcmp(a, b) == 0;
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
  if (CMD_Compare(tokens[1], "INIT") && token_count >= 4) {
    CMD_INIT(tokens[2], tokens[3]);
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

/*起始指令类*/
// CMD_INIT: AT+INIT+<设备名>+<密码>
void CMD_INIT(char *device_name, char *password) {
  // 导航路径: 起始 -> 设备初始化
}

// CMD_ECHO: AT+ECHO
void CMD_ECHO(void) {
  // 导航路径: 起始 -> 设备检测
}

// CMD_INFO: AT+INFO
void CMD_INFO(void) {
  // 导航路径: 起始 -> 获取设备信息
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