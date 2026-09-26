#ifndef __COMMAND_PARSER_H__
#define __COMMAND_PARSER_H__

#include "AI8G.h"
#include "Serial.h"
#include "HW_24c64.h"
#include "Auth.h"
#include "ConfigMgr.h"
#include "StorageMgr.h"

/*
 * ================================================================
 * 命令解析器 
 * ================================================================
 * 与 storage_mgr.h 双 EEPROM 架构同步：
 *   第一块 24C64 (0x50)：配置区 / 块目录 / 键值索引 / 位图
 *   第二块 24Cxx (0x51)：数据片，容量 24C32~24C512
 *
 * 指令格式：AT+<指令>+<参数>...
 * 分隔符：'+'
 * 行结束：\r\n 或 '\0'
 * ================================================================
 */

/* ================================================================
 * 一、起始指令类
 * ================================================================ */

/*
 * AT+INIT+<设备名>+<密码|UPASS>+<加密算法>+<数据片容量代码>
 *   设备名      : 1~17 Byte
 *   密码        : UPASS 表示不启用密码，否则明文，设备端 SHA256 存储
 *   加密算法    : 见 ENC_ALGO_* 枚举
 *                 0=NONE, 1=AES128_CBC, 2=AES128_GCM,
 *                 3=XOR,  4=CAESAR,    5=RC4
 *   容量代码    : 见 DATA_SIZE_* 枚举
 *                 1=24C32, 2=24C64, 3=24C128, 4=24C256, 5=24C512
 *
 * 返回：RESULT+0  初始化成功
 *       RESULT+1  失败
 *       ERR+4     参数错误
 *       ERR+6     第二块容量代码非法
 */
void CMD_INIT(char *device_name, char *password,
              uint8_t encrypt_type, uint8_t data_size_code);

/*
 * AT+ECHO
 *   5s 回响周期；若 5s 内有交互指令则被替代。
 * 返回：RESULT+0 在线
 *       RESULT+1 失败
 *       RESULT+2 未设置密码（沿用旧语义）
 */
void CMD_ECHO(void);

/*
 * AT+INFO
 * 返回：INFO+<设备名>+<密码状态>+<接入计数>+<块数量>+<键值对数量>
 *            +<加密算法>+<固件版本>+<第一块容量>+<第二块容量>
 *            +<第二块片选>+<第二块页大小>
 */
void CMD_INFO(void);

/*
 * AT+STATUS
 * 返回：STATUS+<当前任务>
 *   0 空闲  1 格式化  2 读取    3 写入    4 创建
 *   5 删除  6 更新    7 全获取  8 位图整理 9 统计刷新
 */
void CMD_STATUS(void);

/*
 * AT+USAGE
 * 返回：USAGE+<总容量>+<已用>+<空闲>+<水位>+<下一地址>+<使用率>
 */
void CMD_USAGE(void);

/*
 * AT+ENCINFO
 * 返回：ENCINFO+<算法>+<模式>+<槽大小>+<IV长度>+<TAG长度>
 */
void CMD_ENCINFO(void);

/*
 * AT+VERSION
 * 返回：VERSION+<固件版本>+<协议版本>+<存储格式版本>
 */
void CMD_VERSION(void);

/* ================================================================
 * 二、身份验证指令类
 * ================================================================ */

/*
 * AT+AUTH+PASSWORD+CREATE+<密码>
 *   创建或更新密码。设备端只存 SHA256 摘要。
 * 返回：AUTH+0 成功，AUTH+1 失败
 */
void CMD_AUTH_CREATE(char *password);

/*
 * AT+AUTH+PASSWORD+VERIFY+<密码>
 * 返回：AUTH+0 成功，AUTH+1 失败，AUTH+2 未设置密码
 * 成功后置会话状态为已验证，并启动超时计时。
 */
void CMD_AUTH_VERIFY(char *password);

/*
 * AT+AUTH+PASSWORD+ENABLE
 *   启用密码验证。启用后除 AUTH 与 ECHO 外所有指令需先验证。
 * 返回：AUTH+0 成功
 */
void CMD_AUTH_ENABLE(void);

/*
 * AT+AUTH+PASSWORD+DISABLE
 * 返回：AUTH+0 成功
 */
void CMD_AUTH_DISABLE(void);

/*
 * AT+AUTH+PASSWORD+VERIFYOUT
 *   退出当前会话验证状态。
 * 返回：AUTH+0 成功
 */
void CMD_AUTH_VERIFYOUT(void);

/*
 * AT+AUTH+PASSWORD+CHANGE+<旧密码>+<新密码>
 *   修改密码。旧密码校验通过后写入新 SHA256 摘要。
 * 返回：AUTH+0 成功，AUTH+1 失败
 */
void CMD_AUTH_CHANGE(char *old_pass, char *new_pass);

/*
 * AT+AUTH+PASSWORD+RESET+<恢复密钥>
 *   出厂恢复密码。仅当恢复密钥匹配时生效。
 * 返回：AUTH+0 成功，AUTH+1 失败
 */
void CMD_AUTH_RESET(char *recovery_key);

/* ================================================================
 * 三、功能性 CURD 指令类
 * 失败返回：ERR+<错误码>
 * ================================================================ */

/* ---- 读取 ---- */

/*
 * AT+READ+<读取单位>
 *   通用读取入口，按 <读取单位> 分派。
 */
void CMD_READ(char *unit);

/*
 * AT+READ+BLOCK
 *   读取全部块，返回：
 *     DATA+[块名;块ID](键值对)|[块名;块ID](键值对)|...
 *   键值对格式：<KEY>=<VALUE>
 *   二进制/密文内容由单片机转换成 '\0' 或 '0x1F' 分隔后再输出。
 */
void CMD_READ_ALL_BLOCK(void);

/*
 * AT+READ+BLOCK+<块ID>
 *   读取指定块。
 *   块ID：1~65535，2 Byte 小端十进制
 */
void CMD_READ_BLOCK(char *block_id);

/*
 * AT+READ+KEY+<块ID>+<KEY>
 */
void CMD_READ_KEY(char *block_id, char *key);

/*
 * AT+READ+BLOCK+NAME+<块名>
 *   按块名读取。
 */
void CMD_READ_BLOCK_NAME(char *block_name);

/* ---- 写入 ---- */

/*
 * AT+WRITE+<块ID>+<KEY>+<VALUE>
 *   已存在则覆盖，不存在则追加。
 *   写入前检查第二块空闲空间与位图。
 */
void CMD_WRITE(char *block_id, char *key, char *key_value);

/*
 * AT+WRITE+RAW+<块ID>+<KEY>+<HEX>
 *   直接写入二进制 HEX（'A1B2C3'），不做字符串转换。
 */
void CMD_WRITE_RAW(char *block_id, char *key, char *hex);

/* ---- 创建 ---- */

/*
 * AT+CREATE+BLOCK+<块名>+<块大小|USIZE>
 *   块大小单位为“键值对数量”，USIZE 表示使用默认 16。
 *   分配 block_id，写入第一块块目录，初始化 kv_index。
 */
void CMD_CREATE_BLOCK(char *block_name, char *block_size);

/*
 * AT+CREATE+KEY+<块标识>+<块ID或块名>+<KEY>+<VALUE>
 *   块标识：0=按块名，1=按块ID
 */
void CMD_CREATE_KEY(char *block_flag, char *block_identifier,
                    char *key, char *value);

/* ---- 删除 ---- */

/*
 * AT+DELETE+BLOCK+<块ID>
 *   删除块目录项、kv_index，并把数据片对应位图标记为已删除。
 */
void CMD_DELETE_BLOCK(char *block_id);

/*
 * AT+DELETE+BLOCK+NAME+<块名>
 */
void CMD_DELETE_BLOCK_NAME(char *block_name);

/*
 * AT+DELETE+KEY+<块标识>+<块ID或块名>+<KEY>
 */
void CMD_DELETE_KEY(char *block_flag, char *block_identifier, char *key);

/* ---- 更新 ---- */

/*
 * AT+UPDATE+BLOCK+<块ID>+<新块名>
 *   仅更新块名与属性，不搬移数据。
 */
void CMD_UPDATE_BLOCK(char *block_id, char *new_name);

/*
 * AT+UPDATE+KEY+<块ID>+<KEY>+<VALUE>
 *   等长原地覆盖；若新值更长，则重新分配数据片空间。
 */
void CMD_UPDATE_KEY(char *block_id, char *key, char *key_value);

/* ================================================================
 * 四、全获取指令类
 * ================================================================ */

/*
 * AT+GET+ALL+BLOCK
 *   返回：DATA+[块名;块ID](键值对)|[块名;块ID](键值对)|...
 *   对应四个数组：
 *     【块名数组】 每项 20 Byte
 *     【块ID数组】 每项 2 Byte
 *     【键值对地址二维数组】 每项 kv_entry_t
 *     【键值对二维数组】 KEY_LEN[2]+VAL_LEN[2]+KEY+VALUE
 */
void CMD_GET_ALL_BLOCK(void);

/*
 * AT+GET+ALL+KEY+<块ID>
 *   仅返回指定块的键值对数组。
 */
void CMD_GET_ALL_KEY(char *block_id);

/*
 * AT+GET+SIZE
 *   返回第一块、第二块容量与使用情况。
 */
void CMD_GET_SIZE(void);

/* ================================================================
 * 五、格式化指令类
 * ================================================================ */

/*
 * AT+FORMAT+DEV
 *   两片 EEPROM 全部写 0x00 / 0xFF，
 *   清除状态标志，重建位图，重置使用统计。
 *   保留恢复密钥。
 */
void CMD_FORMAT_DEV(void);

/*
 * AT+FORMAT+BLOCK+<块标识>+<块ID或块名>
 *   仅清除该块目录、kv_index 与数据片占用位。
 *   不重算全片校验和。
 */
void CMD_FORMAT_BLOCK(char *block_flag, char *block_identifier);

/*
 * AT+DEFRAG
 *   整理第二块数据片碎片，重排 KV_RECORD，刷新位图。
 *   返回：RESULT+0 成功
 */
void CMD_DEFRAG(void);

/*
 * AT+REFRESH
 *   重算第一块配置区校验和、第二块数据片头校验和，
 *   刷新使用统计。掉电或异常后建议执行。
 */
void CMD_REFRESH(void);

/* ================================================================
 * 六、加密相关指令
 * ================================================================ */

/*
 * AT+ENC+SET+<算法>
 *   切换全局加密算法。仅影响后续写入；已存数据记录原算法。
 */
void CMD_ENC_SET(char *algo);

/*
 * AT+ENC+KEY+<KEY_HEX>
 *   设置主密钥。设备端仅存密文/摘要，不存明文。
 *   用于 AES128 / RC4 / XOR / 凯撒 的密钥派生。
 */
void CMD_ENC_KEY(char *key_hex);

/*
 * AT+ENC+SALT+<SALT_HEX>
 *   设置 16 Byte 密钥派生盐。
 */
void CMD_ENC_SALT(char *salt_hex);

/*
 * AT+ENC+KDF+<KDF算法>
 *   0=NONE, 1=SHA256, 2=HKDF-SHA256, 3=PBKDF2-SHA256
 */
void CMD_ENC_KDF(char *kdf_algo);

/* ================================================================
 * 七、批量指令类
 * ================================================================ */

/*
 * AT+BATCH+WRITE+<块ID>+<COUNT>
 *   后跟 COUNT 行 KEY=VALUE
 */
void CMD_BATCH_WRITE(char *block_id, char *count);

/*
 * AT+BATCH+DELETE+<块ID>+<COUNT>
 *   后跟 COUNT 行 KEY
 */
void CMD_BATCH_DELETE(char *block_id, char *count);

/* ================================================================
 * 八、错误码 / 状态码 / 返回类型
 * ================================================================ */

/*
 * 返回类型：
 *   DATA+<数据>           字符串数据
 *   ERR+<错误码>          错误
 *   INFO+<...>            设备信息
 *   STATUS+<任务码>       当前任务
 *   RESULT+<结果>         0成功 1失败
 *   AUTH+<验证结果>       0成功 1失败 2未设置密码
 *   USAGE+<...>           使用统计
 *   ENCINFO+<...>         加密描述
 *   VERSION+<...>         版本信息
 */

/* 错误码定义见 StorageMgr.h (ERR_*) */

/* 状态任务码 */
#define TASK_IDLE               0   /* 空闲 */
#define TASK_FORMAT             1   /* 格式化 */
#define TASK_READ               2   /* 读取 */
#define TASK_WRITE              3   /* 写入 */
#define TASK_CREATE             4   /* 创建 */
#define TASK_DELETE             5   /* 删除 */
#define TASK_UPDATE             6   /* 更新 */
#define TASK_GET_ALL            7   /* 全获取 */
#define TASK_DEFRAG             8   /* 位图整理 */
#define TASK_REFRESH            9   /* 统计刷新 */
#define TASK_ENC_SET            10  /* 加密设置 */
#define TASK_BATCH              11  /* 批量操作 */

/* ================================================================
 * 九、内部辅助
 * ================================================================ */

/* 设置当前存储访问地址（兼容旧接口） */
void CMD_PARSER_SET_STORAGE(uint16_t addr);

/* 主入口：解析串口传入的 AT 指令字符串并导航到对应函数 */
void CMD_Parser(char *cmd);

/* 会话状态查询：是否已通过密码验证 */
uint8_t CMD_IsAuthorized(void);

/* 会话超时处理：由定时器周期调用 */
void CMD_SessionTick(void);

#endif /* __COMMAND_PARSER_H__ */