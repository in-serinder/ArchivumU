#ifndef __STORAGE_MGR_H__
#define __STORAGE_MGR_H__

#include "HW_24c64.h"

/**********************************************************************
 * 双 EEPROM 架构
 * ---------------------------------------------------------------
 * 第一块 24C64 (0x50)：元数据片
 *      - 配置区
 *      - 块目录区
 *      - 键值索引区
 *      - 第二块容量描述 / 使用统计
 *      - 数据片分配位图
 *
 * 第二块 24Cxx (0x51, 容量可变)：数据片
 *      - 真正存储 key 对应的 value（明文或密文）
 *      - 容量支持 24C32 / 24C64 / 24C128 / 24C256 / 24C512
 *
 * key  = 索引，存第一块
 * value = 数据，存第二块
 * block = 逻辑分组，目录在第一块
 *
 * 修复点：
 *  1. 0x51~0xFF 长度修正为 175 Byte（原写 178 错误）
 *  2. 加密算法由 bit0~bit3 改为枚举值
 *  3. 24Cxx 片内地址统一 2 Byte 小端，不再用“低四位”
 *  4. 块记录改为变长，BLOCK_SIZE 表示整个块物理总长
 *  5. 引入统一加密描述符，AES/RC4/XOR/凯撒 密文长度统一描述
 *  6. 键值对解析改用长度字段，禁止扫描 0x1F / 0x03
 *  7. 新增第二块容量代码、页大小、已用/空闲/水位/下一可用地址
 *  8. 键值对 BLOCK_ID 由 1Byte 改为 2Byte
 **********************************************************************/

/* ============================================================
 * 片选地址定义
 * ============================================================ */
#define IC_0_24C64          0x50    /* 第一块：元数据片 */
#define IC_1_24CXX          0x51    /* 第二块：数据片，容量可变 */

#define CFG_ZONE_SIZE       0x100   /* 配置区总长 256B */
#define DEV_EEPROM_SIZE     0x2000  /* 单片 24C64 容量 8KB，地址 0x0000~0x1FFF */

/* 第二块容量代码 (0x52) */
#define DATA_SIZE_NONE      0x00    /* 未指定 */
#define DATA_SIZE_24C32     0x01    /* 4KB  */
#define DATA_SIZE_24C64     0x02    /* 8KB  */
#define DATA_SIZE_24C128    0x03    /* 16KB */
#define DATA_SIZE_24C256    0x04    /* 32KB */
#define DATA_SIZE_24C512    0x05    /* 64KB */
#define DATA_SIZE_MAX       0x05

/* ============================================================
 * 第一块 24C64 (0x50) 配置区布局 0x00~0xFF
 * ============================================================ */
#define CFG_ADDR_CHECKSUM       0x00    /* 1B   配置区校验和 */
#define CFG_ADDR_FLAGS          0x01    /* 1B   状态标志位 */
#define CFG_ADDR_PWD_HASH       0x02    /* 32B  密码 SHA256，0x02~0x21 */
#define CFG_ADDR_ACCESS_CNT     0x22    /* 2B   接入计数 uint16 小端 0x22~0x23 */
#define CFG_ADDR_RESERVED       0x24    /* 24B  系统保留区 0x24~0x3B */
#define CFG_ADDR_NAME           0x3C    /* 18B  设备名称 0x3C~0x4D */
#define CFG_ADDR_ENC_ALGO       0x4E    /* 1B   加密算法枚举 */
#define CFG_ADDR_KEY_VOL        0x4F    /* 1B   每块键值对数量，默认16，最大64 */
#define CFG_ADDR_DEV_SIZE       0x50    /* 1B   第一块设备大小 DEVICE_SIZE*1024 */

/* ---- 第二块数据片描述 ---- */
#define CFG_ADDR_DATA_SLAVE     0x51    /* 1B   第二块片选，默认 0x51 */
#define CFG_ADDR_DATA_SIZE_CODE 0x52    /* 1B   第二块容量代码 DATA_SIZE_* */
#define CFG_ADDR_DATA_TOTAL     0x53    /* 2B   第二块总容量 Byte 小端 0x53~0x54 */
#define CFG_ADDR_DATA_PAGE      0x55    /* 2B   第二块页大小 Byte 小端 0x55~0x56 */
#define CFG_ADDR_DATA_USED      0x57    /* 2B   第二块已用字节 小端 0x57~0x58 */
#define CFG_ADDR_DATA_FREE      0x59    /* 2B   第二块空闲字节 小端 0x59~0x5A */
#define CFG_ADDR_DATA_HWM       0x5B    /* 2B   第二块最高写入水位 小端 0x5B~0x5C */
#define CFG_ADDR_DATA_NEXT      0x5D    /* 2B   第二块下一可用地址 小端 0x5D~0x5E */
#define CFG_ADDR_DATA_USAGE     0x5F    /* 1B   第二块使用率 0~100 */

/* ---- 第一块内部逻辑分区 ---- */
#define CFG_ADDR_BITMAP_FMT     0x60    /* 1B   数据片分配表格式版本 */
#define CFG_ADDR_BITMAP_OFF     0x61    /* 2B   分配位图偏移 小端 0x61~0x62 */
#define CFG_ADDR_BITMAP_LEN     0x63    /* 2B   分配位图长度 小端 0x63~0x64 */
#define CFG_ADDR_BLOCKTAB_OFF   0x65    /* 2B   块目录偏移 小端 0x65~0x66 */
#define CFG_ADDR_BLOCKTAB_LEN   0x67    /* 2B   块目录长度 小端 0x67~0x68 */
#define CFG_ADDR_KVIDX_OFF      0x69    /* 2B   键值索引区偏移 小端 0x69~0x6A */
#define CFG_ADDR_KVIDX_LEN      0x6B    /* 2B   键值索引区长度 小端 0x6B~0x6C */
#define CFG_ADDR_KVIDX_CNT      0x6D    /* 2B   键值索引项数量 小端 0x6D~0x6E */
#define CFG_ADDR_KVIDX_ENTRY    0x6F    /* 1B   键值索引项大小 */
#define CFG_ADDR_SALT           0x70    /* 16B  密钥派生盐 0x70~0x7F */
#define CFG_ADDR_KDF_ALGO       0x80    /* 1B   KDF 算法 */
#define CFG_ADDR_EXT            0x81    /* 扩展区 0x81~0xFF，长度 127B */

#define CFG_NAME_LEN        18
#define CFG_HASH_LEN        32
#define CFG_RESERVED_LEN    24
#define CFG_SALT_LEN        16

/* ---- 第一块内部逻辑分区默认偏移 ---- */
#define FIRST_BLOCK_CFG_OFF     0x0000  /* 配置区 256B */
#define FIRST_BLOCK_BLOCKTAB    0x0100  /* 块目录区 */
#define FIRST_BLOCK_KVIDX       0x1000  /* 键值索引区 */
#define FIRST_BLOCK_BITMAP      0x1C00  /* 分配位图区 */
/* 0x1F00~0x1FFF 保留给将来扩展 */

/* ---- 状态标志位 (0x01) ---- */
#define FLAG_INITIALIZED   (1 << 0)  /* bit0：设备是否初始化 */
#define FLAG_READ_ONLY     (1 << 1)  /* bit1：全局只读锁 */
#define FLAG_READ_LOCK     (1 << 2)  /* bit2：读取锁定 */
#define FLAG_PWD_AUTH      (1 << 3)  /* bit3：密码鉴权功能启用 */
#define FLAG_FILE_ENCRYPT  (1 << 4)  /* bit4：文件加密功能启用 */

/* ---- 加密算法枚举 (0x4E)，取代原 bit0~bit3 ---- */
#define ENC_ALGO_NONE           0x00
#define ENC_ALGO_AES128_CBC     0x01
#define ENC_ALGO_AES128_GCM     0x02
#define ENC_ALGO_XOR            0x03
#define ENC_ALGO_CAESAR         0x04
#define ENC_ALGO_RC4            0x05
/* 0x06~0xFF 保留 */

/* ---- KDF 算法 (0x80) ---- */
#define KDF_NONE                0x00
#define KDF_SHA256              0x01
#define KDF_HKDF_SHA256         0x02
#define KDF_PBKDF2_SHA256       0x03

/* ---- 初始化默认参数 ---- */
#define KEY_VOL_DEFAULT     16
#define KEY_VOL_MAX         64
#define DEV_SIZE_DEFAULT    8       /* 8 * 1024 = 8KB(24C64) */
#define PASS_NONE_STR       "UPASS"

/* ============================================================
 * 加密描述符（放在块头或键值对头中）
 * 统一描述所有算法的密文长度、IV、TAG、槽大小
 * ============================================================ */
#define ENC_MODE_STREAM     0x00    /* 流密码：密文长度 == 明文长度 */
#define ENC_MODE_BLOCK_PAD  0x01    /* 分组 + PKCS7 填充 */
#define ENC_MODE_AEAD       0x02    /* 认证加密：密文 = 明文 + TAG */

typedef struct {
    uint8_t  algo;          /* ENC_ALGO_* */
    uint8_t  mode;          /* ENC_MODE_* */
    uint16_t slot_size;     /* 加密负载物理槽大小：0/128/256/512，0=紧贴 */
    uint16_t enc_offset;    /* 密文相对块首偏移 */
    uint16_t enc_len;       /* 实际密文长度（含 TAG，不含 IV） */
    uint16_t plain_len;     /* 解密后明文长度（压缩场景为压缩后长度） */
    uint8_t  iv_len;        /* IV / Nonce 长度 */
    uint8_t  iv[16];        /* IV / Nonce */
    uint8_t  tag_len;       /* TAG / HMAC 长度 */
    uint8_t  tag[16];       /* TAG / HMAC */
    uint8_t  reserved[16];  /* 预留，凑齐 60 Byte */
} enc_desc_t;               /* 物理 60 Byte */

/* 算法属性表：驱动加解密与长度计算 */
typedef struct {
    uint8_t  algo;
    uint8_t  mode;
    uint8_t  iv_len;
    uint8_t  tag_len;
    uint8_t  block_size;    /* 流密码为 1 */
    uint8_t  keep_length;   /* 1=密文长度等于明文长度 */
} enc_algo_info_t;

/* ============================================================
 * 块目录项（第一块内部块目录区）
 * ============================================================ */
#define BLOCK_NAME_LEN      20
#define BLOCK_FLAG_ENCRYPT  (1 << 0)
#define BLOCK_FLAG_READONLY (1 << 1)
#define BLOCK_FLAG_COMPRESS (1 << 2)

typedef struct {
    uint16_t block_id;      /* 块 ID */
    uint8_t  name[BLOCK_NAME_LEN]; /* 块名 */
    uint8_t  flags;         /* 块标志 */
    uint16_t kv_count;      /* 本块键值对数量 */
    uint16_t kv_index_off;  /* 本块键值索引在索引区的偏移 */
    uint16_t kv_index_len;  /* 本块键值索引长度 */
    uint32_t data_start;    /* 本块在数据片起始地址 */
    uint32_t data_used;     /* 本块在数据片已用字节 */
} block_entry_t;

/* ============================================================
 * 键值索引项（第一块键值索引区）
 * 对应 key -> 第二块数据地址
 * ============================================================ */
#define KV_FLAG_ENCRYPTED   (1 << 0)
#define KV_FLAG_VALID       (1 << 1)
#define KV_FLAG_COMPRESS    (1 << 2)

typedef struct {
    uint16_t block_id;      /* 归属块 ID，原 1Byte 不够 */
    uint16_t key_len;       /* key 长度 */
    uint16_t key_off;       /* key 存储偏移（在第一块键值索引区之后） */
    uint8_t  slave;         /* 数据所在片选：0x51 默认 */
    uint16_t data_addr;     /* value 在第二块片内地址 */
    uint16_t data_len;      /* value 密文/明文长度 */
    uint8_t  enc_algo;      /* 该 value 使用的加密算法 */
    uint8_t  flags;         /* 有效/加密/压缩 */
} kv_entry_t;

/* ============================================================
 * 第二块数据片结构
 * 头部 16 Byte，用于自描述
 * ============================================================ */
#define DATA_HEAD_MAGIC     0x5A
#define DATA_HEAD_VER       0x01
#define DATA_HEAD_SIZE      16

/* 数据片头 0x51:0x0000~0x000F
 * 0x00 : 1B  魔数 0x5A
 * 0x01 : 1B  格式版本
 * 0x02 : 1B  容量代码 DATA_SIZE_*
 * 0x03 : 2B  总容量 小端
 * 0x05 : 2B  页大小 小端
 * 0x07 : 2B  已用字节 小端
 * 0x09 : 2B  最高水位 小端
 * 0x0B : 2B  下一可用地址 小端
 * 0x0D : 1B  使用率 0~100
 * 0x0E : 1B  保留
 * 0x0F : 1B  头部校验和
 */

/* ============================================================
 * 键值对数据记录（第二块数据片内）
 * 物理布局：
 * 0x00 : 魔数 0xA5
 * 0x01 : 状态：0x01 有效，0x00 已删除，0xFF 空闲
 * 0x02 : BLOCK_ID[2]
 * 0x04 : KEY_LEN[2]
 * 0x06 : VAL_LEN[2]
 * 0x08 : ENC_ALGO[1]
 * 0x09 : FLAGS[1]
 * 0x0A : IV_LEN[1]
 * 0x0B : IV[16]
 * 0x1B : TAG_LEN[1]
 * 0x1C : TAG[16]
 * 0x2C : KEY[KEY_LEN]
 * ...  : VALUE[VAL_LEN]
 * ...  : CRC16[2]
 * ...  : VER[1]
 * ...  : ETX[1] = 0x03
 * ============================================================ */
#define KV_REC_MAGIC        0xA5
#define KV_REC_STATE_FREE   0xFF
#define KV_REC_STATE_VALID  0x01
#define KV_REC_STATE_DEL    0x00
#define KV_REC_VER          0x01
#define KV_REC_ETX          0x03
#define KV_REC_HDR_LEN      44      /* 0x00~0x2B 头长度 */

#define KV_OFF_BLOCK_ID     0x02
#define KV_OFF_KEY_LEN      0x04
#define KV_OFF_VAL_LEN      0x06
#define KV_OFF_ENC_ALGO     0x08
#define KV_OFF_FLAGS        0x09
#define KV_OFF_IV_LEN       0x0A
#define KV_OFF_IV           0x0B
#define KV_OFF_TAG_LEN      0x1B
#define KV_OFF_TAG          0x1C
#define KV_OFF_KEY          0x2C

/* ============================================================
 * 数据片分配表（位图）
 * 每 1 bit 表示 1 个最小分配单元（默认 16 Byte）
 * ============================================================ */
#define DATA_ALLOC_UNIT     16      /* 最小分配单元 16 Byte */
#define BITMAP_FMT_VER      0x01

/* ============================================================
 * 使用统计结构（运行时缓存，最终落盘到配置区）
 * ============================================================ */
typedef struct {
    uint8_t  slave;         /* 第二块片选 */
    uint8_t  size_code;     /* DATA_SIZE_* */
    uint16_t total_bytes;   /* 总容量 */
    uint16_t page_size;     /* 页大小 */
    uint16_t used_bytes;    /* 已用 */
    uint16_t free_bytes;    /* 空闲 */
    uint16_t high_water;    /* 最高写入水位 */
    uint16_t next_addr;     /* 下一可用地址 */
    uint8_t  usage_percent; /* 0~100 */
} data_store_stat_t;

/* ============================================================
 * AT 指令预处理
 * ---------------------------------------------------------------
 * 设备上线后等待主机发送 AT 指令初始化设备。
 *
 * -> 设备未初始化
 *    AT+INIT+<设备名>+<密码|UPASS>+<数据片容量代码>
 *      对两片 EEPROM 全部写入 0x00 / 0xFF，
 *      初始化状态标志、第二块容量描述、使用统计、分配位图。
 *
 * -> 设备已初始化
 *    AT+ECHO
 *        检测设备是否在线，5s 回响周期；
 *        若 5s 内存在交互指令，则交互指令替代回响。
 *
 *    AT+READ+BLOCK
 *        读取所有块，返回：
 *        1) 块名数组      每项 20 Byte
 *        2) 块 ID 数组    每项 2 Byte
 *        3) 键值对二维数组 每项 KEY_LEN[2] + VAL_LEN[2] + KEY + VALUE
 *
 * 注意：
 *    密文/二进制记录中禁止扫描 0x1F 和 0x03 作为分隔。
 *    所有变长字段必须先读长度，再读数据。
 * ============================================================ */

/* ============================================================
 * 错误码（存储/配置管理层通用）
 * ============================================================ */
#define ERR_OK                  0   /* 成功 */
#define ERR_FAIL                1   /* 失败 */
#define ERR_NO_PASS             2   /* 未设置密码 */
#define ERR_NOT_AUTH            3   /* 未验证 */
#define ERR_PARAM               4   /* 参数错误 */
#define ERR_UNKNOWN             5   /* 未知错误 */
#define ERR_DATA_FULL           6   /* 数据片已满 */
#define ERR_DATA_NOT_INIT       7   /* 数据片未初始化 */
#define ERR_BITMAP_FULL         8   /* 分配位图满 */
#define ERR_BLOCK_NOT_FOUND     9   /* 块不存在 */
#define ERR_KEY_NOT_FOUND       10  /* 键不存在 */
#define ERR_BLOCK_EXIST         11  /* 块已存在 */
#define ERR_KEY_EXIST           12  /* 键已存在 */
#define ERR_SIZE_CODE_INVALID   13  /* 数据片容量代码非法 */
#define ERR_WRITE_PROTECT       14  /* 只读锁 */
#define ERR_CRC_FAIL            15  /* 校验失败 */
#define ERR_ENC_UNSUPPORTED     16  /* 加密算法不支持 */
#define ERR_TIMEOUT             17  /* 超时 */

/* ============================================================
 * 函数声明
 * ============================================================ */
void     StorageMgr_Init(void);
int      StorageMgr_InitDevice(const char *name, const char *pass,
                               uint8_t data_size_code);
void     StorageMgr_InitDataHead(uint16_t total, uint8_t size_code);
void     StorageMgr_Format_BlockZone(void);
void     StorageMgr_Format_EEPROMZone(void);
uint16_t enc_calc_cipher_len(uint8_t algo, uint16_t plain_len);
uint16_t enc_calc_plain_len(uint8_t algo, uint16_t enc_len);

#endif /* __STORAGE_MGR_H__ */