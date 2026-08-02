#ifndef __STORAGE_MGR_H__
#define __STORAGE_MGR_H__
#include "HW_24c64.h"
/*
--->第一片24c64配置区 0x50地址片
固定前几位
0x00      : 1 Byte   校验和，整片配置区数据完整性校验
0x01      : 1 Byte   状态标志位
           bit0：设备是否初始化
           bit1：全局只读锁
           bit2：读取锁定
           bit3：密码鉴权功能启用
           bit4：文件加密功能启用
           bit5~bit7：预留保留位
0x02~0x21 : 32 Byte 设备密码哈希存储区（SHA256完整32字节摘要）
0x22~0x23 :2 Byte   接入计数 uint16，最大65535
0x24~0x3B : 24 Byte  系统保留区
0x3C~0x4D :18 Byte  设备名称存储区
0x4E: 1 Byte  加密方式
           bit0：AES128
           bit1：XOR加密
           bit2：凯撒加密
           bit3：RC4加密
           bit4~bit7：预留保留位
0x4F: 1 Byte  KEY_VOL块数量 默认16个键值对 最大64个
0x50: 1 Byte  设备大小 以 DEVICE_SIZE*1024 定义 默认24C64
0x51~0xFF : 剩余自由扩展区，总长度 178
字节，用于后续功能扩展


// 寻址块空间定义 单块占用57byte 第一片24cxx（0x50）配置区 最大默认16个键值对
BLOCK_NAME: 20byte 块名
BLOCK_ID: 2byte 块ID
BLOCK_SIZE: 2byte 块地址记录大小默认值 0x39+<BLOCK_ARR> 大小标识整个块记录的大小
        后续可能扩展块大小用于键值对地址扩充 
KEY_ADDR: 32byte 连续空间 2byte 每个键值对的地址起始位 默认16个键值对 
"0x1F"区分key和value 两个字符串后截至 发送到上位机时需要通过单片机替换为'\0'字符
BLOCK_ARR:5*KEY_VOL(键值对数量)byte 初始化后整个区块写0xff 写入和读取逻辑 [写入高位一字节通过0x0 表示第一片 0x1 表示第二片] 后面低四位为24c64片内地址 0x0000 ~ 0x1FFF BLOCK_VER: 1byte 校验位 0x01
ETX: 1byte 块结束标志 用于标识一个块的结束 0x03

---> EEPROM内块结构定义 0x51地址片

// 键值对定义
BLOCK_SLAVE: 1byte 块从地址 键值对归属的块ID
KEY_VALUE: 整个键值对字符串 通过'0x1F'的 字符串结束分隔 key和value
VER: 1byte 校验位 用于校验键值对的完整性
ETX: 1byte 键值对结束标志 用于标识一个键值对的结束 0x03


//预处理定义
设备上线后等待主机发送AT指令 初始化设备
->设备未经过初始化
--> AT+INIT+<设备名>+<密码(UPASS表示不启用密码)> 初始化设备
对设备EEPROM全部写入0x00 初始化状态标志
->设备已初始化
--> AT+ECHO检测设备是否在线(5s回响周期 如果5s内存在交互指令就替代回响)
--> AT+READ+BLOCK
读取所有块返回三个【块名数组】和【块ID数组】以及【块对应键值对二维数组】
*/

#define IC_0_24C64 0x50
#define IC_1_24C64 0x51

/* ---- 第一片24C64 配置区地址定义 ---- */
#define CFG_ADDR_CHECKSUM   0x00   /* 1B   校验和，整片配置区数据完整性校验 */
#define CFG_ADDR_FLAGS      0x01   /* 1B   状态标志位 */
#define CFG_ADDR_PWD_HASH   0x02   /* 32B  设备密码哈希存储区 */
#define CFG_ADDR_ACCESS_CNT 0x22   /* 2B   接入计数 uint16 最大65535 */
#define CFG_ADDR_NAME       0x3C   /* 18B  设备名称存储区 */
#define CFG_ADDR_ENCRYPT    0x4E   /* 1B   加密方式 */
#define CFG_ADDR_KEY_VOL    0x4F   /* 1B   KEY_VOL块数量 默认16 最大64 */
#define CFG_ADDR_DEV_SIZE   0x50   /* 1B   设备大小 DEVICE_SIZE*1024 */
#define CFG_ADDR_BLOCK_CNT  0x51   /* 1B   块数量(占用自由扩展区0x51~0xFF首字节) */
#define CFG_ZONE_SIZE       0x100  /* 配置区总长 256B (0x00~0xFF) */
#define CFG_NAME_LEN        18     /* 设备名长度 */
#define CFG_HASH_LEN        32     /* 密码哈希区长度 */
#define DEV_EEPROM_SIZE     0x2000 /* 24C64 单片容量 8KB (0x0000~0x1FFF) */

/* 状态标志位 (0x01) */
#define FLAG_INITIALIZED   (0x01 << 0)  /* bit0：设备是否初始化 */
#define FLAG_READ_ONLY     (0x01 << 1)  /* bit1：全局只读锁 */
#define FLAG_READ_LOCK     (0x01 << 2)  /* bit2：读取锁定 */
#define FLAG_PWD_AUTH      (0x01 << 3)  /* bit3：密码鉴权功能启用 */
#define FLAG_FILE_ENCRYPT  (0x01 << 4)  /* bit4：文件加密功能启用 */

/* 加密方式 (0x4E) */
#define ENC_NONE    0   /* 不加密 */
#define ENC_AES     1   /* AES128  (bit0) */
#define ENC_XOR     2   /* XOR加密  (bit1) */
#define ENC_CAESAR  3   /* 凯撒加密 (bit2) */
#define ENC_RC4     4   /* RC4加密  (bit3) */

/* 初始化默认参数 */
#define KEY_VOL_DEFAULT  16     /* 默认键值对数量 */
#define DEV_SIZE_DEFAULT 8      /* 默认设备大小 8*1024 = 8KB(24C64) */
#define PASS_NONE_STR    "UPASS" /* 密码占位符：表示不启用密码 */

// 存储器管理器
void StorageMgr_Init(void);

#endif // __STORAGE_MGR_H__
