#ifndef __COMMAND_PARSER_H__
#define __COMMAND_PARSER_H__

/*
命令解析器
AT+<指令>+<参数>

// 起始指令
AT+INIT+<设备名>+<密码(UPASS表示不启用密码)>
AT+ECHO检测设备是否在线 返回0成功 1失败 2未设置密码
AT+INFO获取设备信息 返回设备名、密码状态、接入计数、块数量、键值对数量
AT+STATUS获取设备状态 当前任务

// 身份验证指令
AT+AUTH+PASSWORD +CREATE+ <密码> // 创建密码 存在则更新
AT+AUTH+PASSWORD +VERIFY+ <密码> // 验证密码 返回0成功 1失败 2未设置密码
AT+AUTH+PASSWORD + ENABLE // 启用密码验证
启用后除了验证密码指令其他指令都需要验证密码 返回NON-V验证失败 AT+AUTH+PASSWORD
+ DISABLE // 禁用密码验证 AT+AUTH+PASSWORD + VERIFYOUT // 退出密码验证状态



-------功能性CURD指令失败返回\EOF------
// 读取指令
AT+READ+<读取单位>
AT+READ+BLOCK+<块ID>
AT+READ+KEY+<BLOCKID>+<KEY>

// 写入指令
AT+WRITE+<块ID>+<KEY>+<KEY_VALUE>

//创建指令
AT+CREATE+BLOCK+<块名>+<块大小（USIZE使用默认值16个key数量）>
AT+CREATE+KEY+<块标识（0使用块名，1使用块ID）>+<块ID或块名>+<键>+<值>


// 删除指令
AT+DELETE+BLOCK+<块ID>
AT+DELETE+KEY+<块标识（0使用块名，1使用块ID）>+<块ID或块名>+<键>+<值>


// 更新指令
AT+UPDATE+BLOCK+<块ID>
AT+UPDATE+KEY+<BLOCKID>+<KEY>+<KEY_VALUE>

// 全获取指令
 AT+GET+ALL+BLOCK //以数组形式返回所有块名和块ID
数组【块名数组】、【块ID数组】、【块对应键值对地址二维数组】、【块对应键值对二维数组】



-------功能性指令失败返回\EOF 持续返回状态------
// 格式化指令
AT+FORMAT+DEV // 格式化设备EEPROM 初始化状态标志 0x00
AT+FORMAT+BLOCK+<块标识（0使用块名，1使用块ID）>+<块ID或块名> // 格式化指定块
初始化状态标志 0x00





*/

/*
// 设备返回类型
DATA+<数据> // 都为字符串类型
ERR+<错误码> // 错误码 0成功 1失败 2未设置密码 3未验证 4参数错误 5未知错误
INFO+<设备名>+<密码状态>+<接入计数>+<块数量>+<键值对数量> //
设备名、密码状态、接入计数、块数量、键值对数量 STATUS+<当前任务> // 当前任务
0空闲 1格式化 2读取 3写入 4创建 5删除 6更新 7全获取 RESULT+<结果> // 结果 0成功
1失败 AUTH+<验证结果> // 验证结果 0成功 1失败 2未设置密码



*/

#endif // __COMMAND_PARSER_H__
