# ArchivumU 架构设计说明（DS.MD）

本文件描述 ArchivumU 下位机固件的系统架构、模块划分、存储布局与运行时数据流。
对外指令协议见 [`CMD.md`](CMD.md)。

---

## 1. 系统概述

ArchivumU 是一个基于 **8051 内核 8 位单片机**的嵌入式文件/键值存储系统。
上位机通过 **UART** 下发 AT 指令，下位机解析命令并操作 **双片 I²C EEPROM**，
实现「块（block）— 键值对（key-value）」两级的持久化存储，并支持可选的密码鉴权与数据加密。

| 项目 | 说明 |
|------|------|
| MCU | 8051 内核（`AI8G` 头文件，8 位，代码区受限） |
| 存储介质 | 双片 I²C EEPROM（第一片 24C64，第二片 24C32~24C512 可变） |
| 通信 | UART1，AT 指令协议 |
| 安全 | FeatTag 口令摘要 + 会话鉴权 + 可选对称加密 |
| 上位机 | `upper_computer_software/ArchivumU/`（可视化配置工具） |

---

## 2. 分层架构

```
┌─────────────────────────────────────────────────────────────┐
│  应用层        main.c                                        │
│                · 上电初始化 · 主循环 · 固件信息宏            │
├─────────────────────────────────────────────────────────────┤
│  命令层        CommandParser.{h,c}                          │
│                · AT 指令解析与导航 · 会话鉴权 · 状态/信息返回 │
├─────────────────────────────────────────────────────────────┤
│  安全层        Security/Auth.h · Security/FeatTag.{h,c}     │
│                · 口令摘要(FeatTag) · 鉴权状态机              │
├─────────────────────────────────────────────────────────────┤
│  存储管理层    StorageMgr.{h,c}                             │
│                · 双 EEPROM 布局 · 块目录 · 键值索引 · 位图   │
│                · 加解密长度计算 · 使用统计                   │
├─────────────────────────────────────────────────────────────┤
│  设备管理层    DevMgr.{h,c}                                 │
│                · 整片格式化 · 接入计数(0x22~0x23)            │
├─────────────────────────────────────────────────────────────┤
│  硬件抽象层    EEPROM/HW_24c64.{h,c} · Serial/Serial.{h,c}  │
│                · I²C 软时序 · EEPROM 读写 · UART1 收发       │
└─────────────────────────────────────────────────────────────┘
```

### 2.1 各层职责

| 层 | 文件 | 职责 |
|----|------|------|
| 应用层 | `main.c` | 上电初始化 UART、设置存储区大小、进入主循环 |
| 命令层 | `CommandParser.h/.c` | 拆分并导航 AT 指令，维护会话鉴权状态与超时 |
| 安全层 | `Auth.h`、`FeatTag.h/.c` | 口令摘要与校验（FeatTag） |
| 存储层 | `StorageMgr.h/.c` | 块/键值的物理布局、目录、索引、位图、统计、加解密长度计算 |
| 设备层 | `DevMgr.h/.c` | 片内格式化、接入计数持久化 |
| 硬件层 | `EEPROM/HW_24c64.h/.c`、`Serial/Serial.h/.c` | I²C 软时序、EEPROM 字节读写、UART 收发 |

---

## 3. 存储架构

### 3.1 双 EEPROM 分工

| 片选 | 器件 | 角色 | 内容 |
|------|------|------|------|
| `0x50` (`IC_0_24C64`) | 24C64（8 KB） | **元数据片** | 配置区、块目录、键值索引、分配位图、容量描述、使用统计 |
| `0x51` (`IC_1_24CXX`) | 24C32~24C512（4~64 KB） | **数据片** | 真正的 value（明文或密文） |

**设计原则**：

- **key = 索引**，存第一片；**value = 数据**，存第二片；
- **block = 逻辑分组**，其目录项存第一片；
- 数据片容量可变，由配置区的容量代码描述，实现「元数据片固定 + 数据片可扩」。

### 3.2 第一块 24C64（0x50）配置区布局（0x00~0xFF，256 Byte）

| 地址 | 长度 | 字段 | 说明 |
|------|------|------|------|
| `0x00` | 1 | `CFG_ADDR_CHECKSUM` | 配置区校验和（全区异或为 0） |
| `0x01` | 1 | `CFG_ADDR_FLAGS` | 状态标志位 |
| `0x02` | 32 | `CFG_ADDR_PWD_HASH` | 密码摘要（FeatTag 实占 16 B，0x12~0x21 保持 0x00） |
| `0x22` | 2 | `CFG_ADDR_ACCESS_CNT` | 接入计数 uint16（小端） |
| `0x24` | 24 | `CFG_ADDR_RESERVED` | 系统保留 |
| `0x3C` | 18 | `CFG_ADDR_NAME` | 设备名称 |
| `0x4E` | 1 | `CFG_ADDR_ENC_ALGO` | 加密算法枚举 `ENC_ALGO_*` |
| `0x4F` | 1 | `CFG_ADDR_KEY_VOL` | 每块键值对数量，默认 16，最大 64 |
| `0x50` | 1 | `CFG_ADDR_DEV_SIZE` | 第一块设备大小 `DEVICE_SIZE*1024` |
| `0x51` | 1 | `CFG_ADDR_DATA_SLAVE` | 第二块片选，默认 0x51 |
| `0x52` | 1 | `CFG_ADDR_DATA_SIZE_CODE` | 第二块容量代码 `DATA_SIZE_*` |
| `0x53` | 2 | `CFG_ADDR_DATA_TOTAL` | 第二块总容量（小端） |
| `0x55` | 2 | `CFG_ADDR_DATA_PAGE` | 第二块页大小（小端） |
| `0x57` | 2 | `CFG_ADDR_DATA_USED` | 第二块已用字节（小端） |
| `0x59` | 2 | `CFG_ADDR_DATA_FREE` | 第二块空闲字节（小端） |
| `0x5B` | 2 | `CFG_ADDR_DATA_HWM` | 第二块最高写入水位（小端） |
| `0x5D` | 2 | `CFG_ADDR_DATA_NEXT` | 第二块下一可用地址（小端） |
| `0x5F` | 1 | `CFG_ADDR_DATA_USAGE` | 第二块使用率 0~100 |
| `0x60` | 1 | `CFG_ADDR_BITMAP_FMT` | 分配位图格式版本 |
| `0x61` | 2 | `CFG_ADDR_BITMAP_OFF` | 分配位图偏移（小端） |
| `0x63` | 2 | `CFG_ADDR_BITMAP_LEN` | 分配位图长度（小端） |
| `0x65` | 2 | `CFG_ADDR_BLOCKTAB_OFF` | 块目录偏移（小端） |
| `0x67` | 2 | `CFG_ADDR_BLOCKTAB_LEN` | 块目录长度（小端） |
| `0x69` | 2 | `CFG_ADDR_KVIDX_OFF` | 键值索引区偏移（小端） |
| `0x6B` | 2 | `CFG_ADDR_KVIDX_LEN` | 键值索引区长度（小端） |
| `0x6D` | 2 | `CFG_ADDR_KVIDX_CNT` | 键值索引项数量（小端） |
| `0x6F` | 1 | `CFG_ADDR_KVIDX_ENTRY` | 键值索引项大小 |
| `0x70` | 16 | `CFG_ADDR_SALT` | 密钥派生盐 |
| `0x80` | 1 | `CFG_ADDR_KDF_ALGO` | KDF 算法 `KDF_*` |
| `0x81` | 127 | `CFG_ADDR_EXT` | 扩展区 |

### 3.3 第一块内部逻辑分区

| 偏移 | 宏 | 区域 |
|------|----|------|
| `0x0000` | `FIRST_BLOCK_CFG_OFF` | 配置区（256 B） |
| `0x0100` | `FIRST_BLOCK_BLOCKTAB` | 块目录区 |
| `0x1000` | `FIRST_BLOCK_KVIDX` | 键值索引区 |
| `0x1C00` | `FIRST_BLOCK_BITMAP` | 分配位图区 |
| `0x1F00`~`0x1FFF` | — | 保留扩展 |

### 3.4 第二块数据片结构

**数据片头（16 Byte）**：

| 偏移 | 长度 | 字段 |
|------|------|------|
| `0x00` | 1 | 魔数 `0x5A` (`DATA_HEAD_MAGIC`) |
| `0x01` | 1 | 格式版本 `0x01` |
| `0x02` | 1 | 容量代码 `DATA_SIZE_*` |
| `0x03` | 2 | 总容量（小端） |
| `0x05` | 2 | 页大小（小端） |
| `0x07` | 2 | 已用字节（小端） |
| `0x09` | 2 | 最高水位（小端） |
| `0x0B` | 2 | 下一可用地址（小端） |
| `0x0D` | 1 | 使用率 0~100 |
| `0x0E` | 1 | 保留 |
| `0x0F` | 1 | 头部校验和 |

**键值对数据记录 KV_RECORD**：

| 偏移 | 长度 | 字段 |
|------|------|------|
| `0x00` | 1 | 魔数 `0xA5` |
| `0x01` | 1 | 状态（`0x01` 有效 / `0x00` 已删除 / `0xFF` 空闲） |
| `0x02` | 2 | `BLOCK_ID` |
| `0x04` | 2 | `KEY_LEN` |
| `0x06` | 2 | `VAL_LEN` |
| `0x08` | 1 | `ENC_ALGO` |
| `0x09` | 1 | `FLAGS` |
| `0x0A` | 1 | `IV_LEN` |
| `0x0B` | 16 | `IV` |
| `0x1B` | 1 | `TAG_LEN` |
| `0x1C` | 16 | `TAG` |
| `0x2C` | `KEY_LEN` | `KEY` |
| … | `VAL_LEN` | `VALUE` |
| … | 2 | `CRC16` |
| … | 1 | `VER` |
| … | 1 | `ETX` = `0x03` |

记录头长度 `KV_REC_HDR_LEN = 44`（`0x00~0x2B`）。

### 3.5 主要数据结构

```c
/* 块目录项 */
typedef struct {
    uint16_t block_id;             /* 块 ID */
    uint8_t  name[BLOCK_NAME_LEN]; /* 块名，20 Byte */
    uint8_t  flags;                /* 加密/只读/压缩 */
    uint16_t kv_count;             /* 本块键值对数量 */
    uint16_t kv_index_off;         /* 键值索引偏移 */
    uint16_t kv_index_len;         /* 键值索引长度 */
    uint32_t data_start;           /* 数据片起始地址 */
    uint32_t data_used;            /* 数据片已用字节 */
} block_entry_t;

/* 键值索引项：key -> 数据片地址 */
typedef struct {
    uint16_t block_id;   /* 归属块 ID（原 1B 改为 2B） */
    uint16_t key_len;    /* key 长度 */
    uint16_t key_off;    /* key 存储偏移 */
    uint8_t  slave;      /* 数据所在片选，默认 0x51 */
    uint16_t data_addr;  /* value 片内地址 */
    uint16_t data_len;   /* value 长度 */
    uint8_t  enc_algo;   /* 该 value 的加密算法 */
    uint8_t  flags;      /* 有效/加密/压缩 */
} kv_entry_t;

/* 加密描述符（块头或键值对头，物理 60 Byte） */
typedef struct {
    uint8_t  algo;         /* ENC_ALGO_* */
    uint8_t  mode;         /* ENC_MODE_* */
    uint16_t slot_size;    /* 物理槽大小：0/128/256/512，0=紧贴 */
    uint16_t enc_offset;
    uint16_t enc_len;
    uint16_t plain_len;
    uint8_t  iv_len;
    uint8_t  iv[16];
    uint8_t  tag_len;
    uint8_t  tag[16];
    uint8_t  reserved[16];
} enc_desc_t;

/* 使用统计（运行时缓存，落盘到配置区） */
typedef struct {
    uint8_t  slave;
    uint8_t  size_code;
    uint16_t total_bytes;
    uint16_t page_size;
    uint16_t used_bytes;
    uint16_t free_bytes;
    uint16_t high_water;
    uint16_t next_addr;
    uint8_t  usage_percent;
} data_store_stat_t;
```

### 3.6 分配位图

- 粒度为 `DATA_ALLOC_UNIT = 16 Byte`，**1 bit 表示 1 个最小分配单元**；
- 位图本体存第一块 `FIRST_BLOCK_BITMAP (0x1C00)` 起，元信息（偏移/长度/格式版本）
  存配置区 `0x60~0x64`。

---

## 4. 状态标志位（`CFG_ADDR_FLAGS`，0x01）

| 位 | 宏 | 含义 |
|----|----|------|
| bit0 | `FLAG_INITIALIZED` | 设备是否已初始化 |
| bit1 | `FLAG_READ_ONLY` | 全局只读锁 |
| bit2 | `FLAG_READ_LOCK` | 读取锁定 |
| bit3 | `FLAG_PWD_AUTH` | 密码鉴权功能启用 |
| bit4 | `FLAG_FILE_ENCRYPT` | 文件加密功能启用 |

---

## 5. 安全设计

### 5.1 FeatTag 口令摘要

- 位置：`firmware_keil/Security/FeatTag.{h,c}`；
- 定位：**面向 8 位 MCU 的紧凑型特征算法（中等安全强度）**，非密码学哈希；
- 输入 6~16 字节字符串 → 输出 16 字节校验码；
- 结构：16 字节状态的 **sponge（吸收—扩散—挤出）**：

  1. **吸收**：逐字节把输入混入状态（位置相关，含加/循环左移/异或）；
  2. **扩散**：`FEATTAG_ROUNDS`（默认 8）轮非线性混淆，每轮前向 + 后向各扫一遍；
  3. **挤出**：状态对折异或再循环左移，输出 16 字节，保证雪崩。

- 代码量 < 1.2 KB，适合代码区受限的 8051。

> 说明：`CommandParser.h` 旧注释写「SHA256 存储」，实现已迁移为 FeatTag 摘要（见 `CMD_INIT` 中的
> `FeatTag_Checksum()` 调用）。BLAKE2s 已被 FeatTag 取代。

### 5.2 会话鉴权

- `CMD_AUTH_VERIFY` 成功后置会话为「已验证」并启动超时；
- `CMD_SessionTick()` 由定时器周期调用，超时后自动失效；
- `CMD_IsAuthorized()` 供各指令入口查询；
- 启用密码验证后，除 `AUTH` 与 `ECHO` 外均需先验证。

### 5.3 数据加密

| 值 | 算法 | 模式 |
|----|------|------|
| 0 | `ENC_ALGO_NONE` | 明文 |
| 1 | `ENC_ALGO_AES128_CBC` | 分组 + PKCS7 填充 (`ENC_MODE_BLOCK_PAD`) |
| 2 | `ENC_ALGO_AES128_GCM` | 认证加密 (`ENC_MODE_AEAD`) |
| 3 | `ENC_ALGO_XOR` | 流密码 (`ENC_MODE_STREAM`) |
| 4 | `ENC_ALGO_CAESAR` | 流密码 |
| 5 | `ENC_ALGO_RC4` | 流密码 |

- 算法属性由 `enc_algo_info_t` 表驱动；
- `enc_calc_cipher_len()` / `enc_calc_plain_len()` 统一计算长度；
- KDF：`KDF_NONE / SHA256 / HKDF-SHA256 / PBKDF2-SHA256`，配合 16 Byte 盐派生密钥。

---

## 6. 运行时数据流

### 6.1 上电流程（`main.c`）

```
上电
  → Uart1_Init()                        初始化 UART1
  → CMD_PARSER_SET_STORAGE(16*1024)     设置存储区大小
  → 进入 while(1) 主循环，等待串口指令
```

### 6.2 指令处理流程

```
主机 → UART1 → CMD_Parser(cmd)
  → CMD_Split(cmd, '+', tokens, 10)     按 '+' 拆分
  → 校验 tokens[0] == "AT"
  → 二级/三级关键字导航
  → 调用具体 CMD_xxx() 处理函数
  → 处理函数读写 EEPROM / 安全层
  → Uart1_SendString / SendNumber 回包
```

### 6.3 设备初始化流程（`CMD_INIT`）

```
AT+INIT 到达
  → 解析设备名 / 密码 / 加密方式
  → 判定 enablePassword（非空且非 "UPASS"）
  → 置 FLAG_INITIALIZED / FLAG_PWD_AUTH / FLAG_FILE_ENCRYPT
  → EEPROM_SetAddress(IC_0_24C64)
  → DM_format_block(DEV_EEPROM_SIZE, 0x00)   整片清零
  → 写 FLAGS / 密码摘要(FeatTag) / 设备名 / 加密方式
    / 键值对数量 / 设备大小
  → 逐字段异或得到校验和，写 CFG_ADDR_CHECKSUM
```

---

## 7. 硬件抽象层

### 7.1 I²C / EEPROM（`EEPROM/HW_24c64.h`）

- 软 I²C：`SDA = P33`、`SCL = P32`；写保护 `IC0_WP = P54`、`IC1_WP = P55`；
- 关键接口：

  | 函数 | 说明 |
  |------|------|
  | `I2C_Init()` | 初始化 I²C |
  | `EEPROM_SetAddress(addr)` | 入参为 7 位设备地址（`0x50`/`0x51`），内部左移为 8 位写地址 |
  | `EEPROM_WriteByte/ReadByte(addr, dat)` | 片内字节读写 |
  | `EEPROM_WriteString/ReadString(...)` | 连续字符串读写 |
  | `HW_EEPROM_ClearAll()` | 整片清零 |

- 作为**设备地址**的 `EEPROM_ADDR` 与作为**存储区大小**的 `s_storage_size` 已明确区分，
  避免旧代码混用（见 `CommandParser.c` 中 `CMD_PARSER_SET_STORAGE()`）。

### 7.2 串口（`Serial/Serial.h`）

| 函数 | 说明 |
|------|------|
| `Uart1_Init()` | 初始化 UART1 |
| `Uart1_SendByte/SendString/SendNumber/SendHex/SendHexBuffer` | 各类发送 |
| `Uart1_ReceiveByte()` | 阻塞接收 1 字节 |
| `Uart1_ReceiveString(buf, max_len, timeout)` | 带超时接收字符串 |

---

## 8. 目录结构

```
firmware_keil/
├── main.c                    应用入口
├── CommandParser.{h,c}       命令解析层
├── StorageMgr.{h,c}          存储管理层
├── DevMgr.{h,c}              设备管理层
├── Security/
│   ├── Auth.h                安全层接口
│   └── FeatTag.{h,c}         口令摘要算法
├── EEPROM/
│   └── HW_24c64.{h,c}        I²C / EEPROM 硬件抽象
├── Serial/
│   └── Serial.{h,c}          UART 硬件抽象
├── Objects/                  Keil 编译产物 (*.obj/*.lst)
├── Listings/                 Keil 列表文件
└── archu.uvproj / .uvopt     Keil 工程文件
```

---

## 9. 设计要点与修复记录

`StorageMgr.h` 中列出的历史修复点（现行设计的约束来源）：

1. `0x51~0xFF` 长度修正为 **175 Byte**（原写 178 错误）；
2. 加密算法由 `bit0~bit3` 改为**枚举值** `ENC_ALGO_*`；
3. 24Cxx 片内地址统一 **2 Byte 小端**，不再用「低四位」；
4. 块记录改为**变长**，`BLOCK_SIZE` 表示整个块物理总长；
5. 引入**统一加密描述符**，AES/RC4/XOR/凯撒密文长度统一描述；
6. 键值对解析改用**长度字段**，禁止扫描 `0x1F` / `0x03`；
7. 新增第二块容量代码、页大小、已用/空闲/水位/下一可用地址；
8. 键值对 `BLOCK_ID` 由 1 Byte 改为 **2 Byte**。

---

## 10. 待办与已知差异

> 与 [`CMD.md` 第 9 节](CMD.md#91-待办与已知差异) 对应，偏架构层面的问题：

1. **命令层未完全接入**：`CMD_Parser()` 只覆盖部分指令，`CMD_READ`/`CMD_STATUS`/`CMD_USAGE`/
   `CMD_ENCINFO`/`CMD_VERSION` 及多数 CURD 处理函数为空实现，需补齐业务逻辑与存储层对接。
2. **`CMD_INIT` 调用签名不一致**：头文件 4 参数，解析器传 3 参数，缺 `data_size_code`。
3. **存储层实现不完整**：`StorageMgr.c` 目前仅有 `StorageMgr_Init` 与格式化桩函数；
   `StorageMgr_InitDevice()`、`enc_calc_*_len()` 均未实现。
4. **`main.c` 主循环为空**：尚未接入串口接收—命令解析回路。
5. **注释与实现漂移**：多处「SHA256」注释应更新为 FeatTag。

> 本文件描述**内部架构**；对外协议格式见 [`CMD.md`](CMD.md)。
