#ifndef __STORAGE_MGR_H__
#define __STORAGE_MGR_H__
#include "24c64.h"
/*
片内结构定义
固定前几位
0x00      : 1 Byte   校验和，整片配置区数据完整性校验
0x01      : 1 Byte   状态标志位
           bit0：设备是否初始化
           bit1：全局只读锁
           bit2：读取锁定
           bit3：密码鉴权功能启用
           bit4：文件加密功能启用
           bit5~bit7：预留保留位
0x02~0x21 : 32 Byte
设备密码哈希存储区（SHA256完整32字节摘要） 0x22~0x23 :
2 Byte   接入计数 uint16，最大65535 0x24~0x3B : 24 Byte  系统保留区 0x3C~0x4D :
18 Byte  设备名称存储区 0x4E~0xFF : 剩余自由扩展区，总长度 178
字节，用于后续功能扩展


// 寻址块空间定义 占用57byte 索引在stc8g08a片内EEPROM 最大默认16个键值对
BLOCK_NAME: 20byte 块名
BLOCK_ID: 2byte 块ID
BLOCK_SIZE: 2byte 块地址记录大小默认值 0x39 标识整个块记录的大小
后续可能扩展块大小用于键值对地址扩充 KEY_ADDR: 32byte 连续空间 2byte
每个键值对的地址起始位 16个键值对 最高位一位用于区分两块24c64片0和1
"0x1F"区分key和value 两个字符串后截至 发送到上位机时需要通过单片机替换为'\0'字符
BLOCK_VER: 1byte 校验位 0x01

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
--> AT+ECHO检测设备是否在线
--> AT+READ+BLOCK
读取所有块返回三个【块名数组】和【块ID数组】以及【块对应键值对二维数组】
*/

#define IC_0_24C64 0x50
#define IC_1_24C64 0x51

// 存储器管理器
void StorageMgr_Init(void);

#endif // __STORAGE_MGR_H__
