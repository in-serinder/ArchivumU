# ArchivumU 命令协议文档（CMD.md）

本文件描述 ArchivumU 下位机固件（`firmware_keil/`）的串口 AT 指令协议。
协议解析入口见 [`firmware_keil/CommandParser.c`](firmware_keil/CommandParser.c) 的 `CMD_Parser()`，
接口声明见 [`firmware_keil/CommandParser.h`](firmware_keil/CommandParser.h)。

---

## 1. 协议概述

| 项目 | 说明 |
|------|------|
| 传输层 | UART1（异步串口） |
| 指令前缀 | `AT` |
| 参数分隔符 | `+` |
| 行结束符 | `\r\n`（设备回包统一以 `\r\n` 结尾） |
| 指令格式 | `AT+<指令>+<参数>...` |
| 大小写 | **区分大小写**，关键字全大写 |

一条指令最多被拆分为 **10 个 token**（见 `CMD_Parser()` 中的 `tokens[10]`）。
解析流程：先校验 `tokens[0] == "AT"`，再按 `tokens[1]` 做二级导航，逐级匹配到具体处理函数。

### 1.1 返回类型前缀

设备所有回包均以如下类型前缀开头：

| 前缀 | 含义 | 示例 |
|------|------|------|
| `DATA+` | 数据内容 | `DATA+[块名;块ID](k=v)\|...` |
| `ERR+` | 错误码 | `ERR+4` |
| `INFO+` | 设备信息 | `INFO+...` |
| `STATUS+` | 当前任务码 | `STATUS+0` |
| `RESULT+` | 结果（0 成功 / 1 失败） | `RESULT+0` |
| `AUTH+` | 验证结果 | `AUTH+0` |
| `USAGE+` | 使用统计 | `USAGE+...` |
| `ENCINFO+` | 加密描述 | `ENCINFO+...` |
| `VERSION+` | 版本信息 | `VERSION+...` |

> 注意：`CMD_INFO()` 在**设备未初始化**时返回 `INIT=0+<固件版本>`，而非 `INFO+`。

### 1.2 错误码（`ERR+<code>`）

| 码 | 宏 | 含义 |
|----|----|------|
| 0 | `ERR_OK` | 成功 |
| 1 | `ERR_FAIL` | 失败 |
| 2 | `ERR_NO_PASS` | 未设置密码 |
| 3 | `ERR_NOT_AUTH` | 未验证 |
| 4 | `ERR_PARAM` | 参数错误 |
| 5 | `ERR_UNKNOWN` | 未知错误 |
| 6 | `ERR_DATA_FULL` | 数据片已满 |
| 7 | `ERR_DATA_NOT_INIT` | 数据片未初始化 |
| 8 | `ERR_BITMAP_FULL` | 分配位图满 |
| 9 | `ERR_BLOCK_NOT_FOUND` | 块不存在 |
| 10 | `ERR_KEY_NOT_FOUND` | 键不存在 |
| 11 | `ERR_BLOCK_EXIST` | 块已存在 |
| 12 | `ERR_KEY_EXIST` | 键已存在 |
| 13 | `ERR_SIZE_CODE_INVALID` | 数据片容量代码非法 |
| 14 | `ERR_WRITE_PROTECT` | 只读锁 |
| 15 | `ERR_CRC_FAIL` | 校验失败 |
| 16 | `ERR_ENC_UNSUPPORTED` | 加密算法不支持 |
| 17 | `ERR_TIMEOUT` | 超时 |

### 1.3 任务状态码（`STATUS+<task>`）

| 码 | 宏 | 含义 |
|----|----|------|
| 0 | `TASK_IDLE` | 空闲 |
| 1 | `TASK_FORMAT` | 格式化 |
| 2 | `TASK_READ` | 读取 |
| 3 | `TASK_WRITE` | 写入 |
| 4 | `TASK_CREATE` | 创建 |
| 5 | `TASK_DELETE` | 删除 |
| 6 | `TASK_UPDATE` | 更新 |
| 7 | `TASK_GET_ALL` | 全获取 |
| 8 | `TASK_DEFRAG` | 位图整理 |
| 9 | `TASK_REFRESH` | 统计刷新 |
| 10 | `TASK_ENC_SET` | 加密设置 |
| 11 | `TASK_BATCH` | 批量操作 |

---

## 2. 起始指令类

### 2.1 `AT+INIT` — 设备初始化

```
AT+INIT+<设备名>+<密码|UPASS>+<加密算法>+<数据片容量代码>
```

| 参数 | 说明 |
|------|------|
| 设备名 | 1~17 Byte（片内保留 18 Byte，不足补 `0x00`） |
| 密码 | `UPASS` 表示不启用密码；否则为明文，设备端 **FeatTag 摘要**后存 16 Byte（不存明文） |
| 加密算法 | 见 [加密算法枚举](#41-加密算法枚举) |
| 数据片容量代码 | 见 [数据片容量代码](#42-数据片容量代码) |

**行为**：对两片 EEPROM 全部写 `0x00` 清零 → 写入状态标志、密码摘要、设备名、加密方式、
键值对数量、设备大小，最后写入配置区校验和。

**返回**：`RESULT+0` 成功 / `RESULT+1` 失败 / `ERR+4` 参数错误 / `ERR+6` 数据片容量非法。

> 实现注意：当前 `CMD_Parser()` 调用 `CMD_INIT(tokens[2], tokens[3], CMD_Atoi(tokens[4]))` 只解析
> **3 个**参数，与头文件声明的 4 参数 `CMD_INIT(..., data_size_code)` 存在签名不一致，
> 待对齐（见 [第 9 节](#9-待办与已知差异)）。

### 2.2 `AT+ECHO` — 在线检测

```
AT+ECHO
```

5s 回响周期；若 5s 内收到交互指令，则被交互指令替代。

**返回**：`RESULT+0` 在线 / `RESULT+1` 失败 / `RESULT+2` 未设置密码。

### 2.3 `AT+INFO` — 读取设备信息

```
AT+INFO
```

**已初始化返回**：

```
INFO+<设备名>+<固件版本>+<密码状态>+<接入计数>+<块数量>+<键值对数量>+<加密方式>+<存储总大小>
```

- 密码状态：`ENABLED` / `DISABLED`
- 加密方式：`NON` / `AES` / `XOR` / `CESAR` / `RC4`
- 存储总大小：`dev_size * 2048`（两片合计，默认 `8 * 2048 = 16384`）

**未初始化返回**：`INIT=0+<固件版本>`

### 2.4 `AT+STATUS` — 读取当前任务

```
AT+STATUS
```

**返回**：`STATUS+<任务码>`（见 [任务状态码](#13-任务状态码statustask)）。

### 2.5 `AT+USAGE` — 使用统计

```
AT+USAGE
```

**返回**：`USAGE+<总容量>+<已用>+<空闲>+<水位>+<下一地址>+<使用率>`

### 2.6 `AT+ENCINFO` — 加密描述

```
AT+ENCINFO
```

**返回**：`ENCINFO+<算法>+<模式>+<槽大小>+<IV长度>+<TAG长度>`

### 2.7 `AT+VERSION` — 版本信息

```
AT+VERSION
```

**返回**：`VERSION+<固件版本>+<协议版本>+<存储格式版本>`

---

## 3. 身份验证指令类

所有身份验证指令的二级关键字为 `AUTH`，三级关键字为 `PASSWORD`。

| 指令 | 格式 | 返回 |
|------|------|------|
| 创建密码 | `AT+AUTH+PASSWORD+CREATE+<密码>` | `AUTH+0` / `AUTH+1` |
| 验证密码 | `AT+AUTH+PASSWORD+VERIFY+<密码>` | `AUTH+0` 成功 / `AUTH+1` 失败 / `AUTH+2` 未设置密码 |
| 启用验证 | `AT+AUTH+PASSWORD+ENABLE` | `AUTH+0` |
| 禁用验证 | `AT+AUTH+PASSWORD+DISABLE` | `AUTH+0` |
| 退出验证 | `AT+AUTH+PASSWORD+VERIFYOUT` | `AUTH+0` |
| 修改密码 | `AT+AUTH+PASSWORD+CHANGE+<旧密码>+<新密码>` | `AUTH+0` / `AUTH+1` |
| 出厂恢复 | `AT+AUTH+PASSWORD+RESET+<恢复密钥>` | `AUTH+0` / `AUTH+1` |

- **创建/修改**：设备端只存 FeatTag 摘要。
- **验证**：成功后置会话为「已验证」并启动超时计时（`CMD_SessionTick()` 由定时器周期调用）。
- **启用后**：除 `AUTH` 与 `ECHO` 外，所有指令需先通过验证（`CMD_IsAuthorized()`）。

---

## 4. 功能性 CURD 指令类

失败统一返回 `ERR+<错误码>`。

### 4.1 读取

| 指令 | 格式 | 说明 |
|------|------|------|
| 读取全部块 | `AT+READ+BLOCK` | 返回 `DATA+[块名;块ID](键值对)\|...` |
| 读取指定块 | `AT+READ+BLOCK+<块ID>` | 块ID 1~65535 |
| 读取键值 | `AT+READ+KEY+<块ID>+<KEY>` | 读取单个键 |
| 按名读取块 | `AT+READ+BLOCK+NAME+<块名>` | 按块名读取 |

键值对格式：`<KEY>=<VALUE>`；块之间以 `|` 分隔。

> 二进制/密文内容由单片机转换为 `\0` 或 `0x1F` 分隔后再输出；
> 解析时**禁止**扫描 `0x1F`/`0x03` 作为分隔，必须按长度字段读取（见 `StorageMgr.h`）。

### 4.2 写入

| 指令 | 格式 | 说明 |
|------|------|------|
| 写入 | `AT+WRITE+<块ID>+<KEY>+<VALUE>` | 存在则覆盖，不存在则追加；写入前检查数据片空闲空间与位图 |
| 写入原始 | `AT+WRITE+RAW+<块ID>+<KEY>+<HEX>` | 直接写入二进制 HEX（如 `A1B2C3`），不做字符串转换 |

### 4.3 创建

| 指令 | 格式 | 说明 |
|------|------|------|
| 创建块 | `AT+CREATE+BLOCK+<块名>+<块大小\|USIZE>` | 块大小单位为「键值对数量」，`USIZE` 表示默认 16 |
| 创建键值 | `AT+CREATE+KEY+<块标识>+<块ID或块名>+<KEY>+<VALUE>` | 块标识：`0`=按块名，`1`=按块ID |

### 4.4 删除

| 指令 | 格式 | 说明 |
|------|------|------|
| 删除块 | `AT+DELETE+BLOCK+<块ID>` | 删除块目录项、kv_index，并标记数据片位图 |
| 按名删除块 | `AT+DELETE+BLOCK+NAME+<块名>` | 按块名删除 |
| 删除键值 | `AT+DELETE+KEY+<块标识>+<块ID或块名>+<KEY>` | 删除单个键 |

### 4.5 更新

| 指令 | 格式 | 说明 |
|------|------|------|
| 更新块 | `AT+UPDATE+BLOCK+<块ID>+<新块名>` | 仅更新块名与属性，不搬移数据 |
| 更新键值 | `AT+UPDATE+KEY+<块ID>+<KEY>+<VALUE>` | 等长原地覆盖；更长则重新分配数据片空间 |

---

## 5. 全获取指令类

| 指令 | 格式 | 说明 |
|------|------|------|
| 全获取块 | `AT+GET+ALL+BLOCK` | 返回 `DATA+[块名;块ID](键值对)\|...` |
| 全获取键 | `AT+GET+ALL+KEY+<块ID>` | 仅返回指定块的键值对数组 |
| 获取容量 | `AT+GET+SIZE` | 返回第一块、第二块容量与使用情况 |

`GET+ALL+BLOCK` 对应四个数组：

| 数组 | 每项大小 |
|------|----------|
| 块名数组 | 20 Byte |
| 块ID数组 | 2 Byte |
| 键值对地址二维数组 | `kv_entry_t` |
| 键值对二维数组 | `KEY_LEN[2] + VAL_LEN[2] + KEY + VALUE` |

---

## 6. 格式化指令类

| 指令 | 格式 | 说明 |
|------|------|------|
| 格式化设备 | `AT+FORMAT+DEV` | 两片 EEPROM 全写 `0x00`/`0xFF`，清状态标志、重建位图、重置统计；**保留恢复密钥** |
| 格式化块 | `AT+FORMAT+BLOCK+<块标识>+<块ID或块名>` | 仅清除该块目录、kv_index 与数据片占用位，不重算全片校验和 |
| 碎片整理 | `AT+DEFRAG` | 整理数据片碎片，重排 KV_RECORD，刷新位图；返回 `RESULT+0` |
| 刷新 | `AT+REFRESH` | 重算两片校验和，刷新使用统计；掉电/异常后建议执行 |

---

## 7. 加密相关指令

| 指令 | 格式 | 说明 |
|------|------|------|
| 设置算法 | `AT+ENC+SET+<算法>` | 切换全局加密算法，仅影响后续写入 |
| 设置主密钥 | `AT+ENC+KEY+<KEY_HEX>` | 设置主密钥（设备端仅存密文/摘要，不存明文） |
| 设置盐 | `AT+ENC+SALT+<SALT_HEX>` | 设置 16 Byte 密钥派生盐 |
| 设置 KDF | `AT+ENC+KDF+<KDF算法>` | KDF：0=NONE, 1=SHA256, 2=HKDF-SHA256, 3=PBKDF2-SHA256 |

---

## 8. 批量指令类

| 指令 | 格式 | 说明 |
|------|------|------|
| 批量写入 | `AT+BATCH+WRITE+<块ID>+<COUNT>` | 后跟 `COUNT` 行 `KEY=VALUE` |
| 批量删除 | `AT+BATCH+DELETE+<块ID>+<COUNT>` | 后跟 `COUNT` 行 `KEY` |

---

## 9. 附录

### 4.1 加密算法枚举

| 值 | 宏 | 算法 |
|----|----|------|
| 0 | `ENC_ALGO_NONE` | 不加密 |
| 1 | `ENC_ALGO_AES128_CBC` | AES-128-CBC |
| 2 | `ENC_ALGO_AES128_GCM` | AES-128-GCM |
| 3 | `ENC_ALGO_XOR` | XOR |
| 4 | `ENC_ALGO_CAESAR` | 凯撒 |
| 5 | `ENC_ALGO_RC4` | RC4 |

> `CommandParser.h` 注释中旧的 `0=NONE,1=AES128_CBC,2=AES128_GCM,3=XOR,4=CAESAR,5=RC4`
> 与 `StorageMgr.h` 的 `ENC_ALGO_*` 枚举一致，以 `StorageMgr.h` 为准。

### 4.2 数据片容量代码

| 值 | 宏 | 容量 |
|----|----|------|
| 0 | `DATA_SIZE_NONE` | 未指定 |
| 1 | `DATA_SIZE_24C32` | 4 KB |
| 2 | `DATA_SIZE_24C64` | 8 KB |
| 3 | `DATA_SIZE_24C128` | 16 KB |
| 4 | `DATA_SIZE_24C256` | 32 KB |
| 5 | `DATA_SIZE_24C512` | 64 KB |

### 9.1 待办与已知差异

以下为文档编写时发现的**代码与头文件/协议不一致点**，建议后续对齐：

1. **`CMD_INIT` 签名不一致**：头文件声明 4 参数 `(name, password, encrypt_type, data_size_code)`，
   但 `CMD_Parser()` 只传 3 个参数（缺 `data_size_code`）。
2. **`CMD_Parser()` 未覆盖全部已声明指令**：`USAGE`、`ENCINFO`、`VERSION`、`READ+BLOCK+NAME`、
   `READ` 无参分支、`CREATE/DELETE/UPDATE/GET/ENC/BATCH` 等多条路径在导航中未接入或部分缺失。
3. **部分处理函数为空实现**（仅有 `// 导航路径` 注释占位），需补齐业务逻辑。
4. **`CMD_UPDATE_BLOCK` / `CMD_CREATE_KEY` 等解析参数个数**：`CMD_Parser()` 中 `UPDATE+BLOCK`
   只传 `tokens[3]`，但头文件为 `(block_id, new_name)` 两参数；`CREATE+KEY` 传 4 参数而头文件为 4 参数
   （`block_flag, block_identifier, key, value`）——需逐一核对。
5. **密码摘要算法**：`CommandParser.h` 注释写「SHA256 存储」，实际实现（`CMD_INIT`）使用
   **FeatTag** 输出 16 Byte，注释需更新。

> 本文件仅描述**对外协议**；内部存储布局与模块划分见 [`DS.MD`](DS.MD)。
