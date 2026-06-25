#ifndef __STORAGE_MGR_H__
#define __STORAGE_MGR_H__
#include "24c64.h"
/*
片内结构定义
固定前几位
0x00：1Byte 校验和 用于校验存储器数据的完整性
0x01：1Byte 状态标志（bit0 初始化、bit1 只读锁、bit2 读锁定、bit3
密码启用，bit4~bit7 保留） 0x02~0x09：8Byte 设备密码 0x0A~0x0B：2Byte
接入计数（uint16，上限 65535，满足需求） 0x0C~0x23：24Byte 系统保留
0x24~0x35：18Byte 设备名存储
0x36~0xFF：剩余自由保留区（220 字节，可后续扩展功能）


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
