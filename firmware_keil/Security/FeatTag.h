#ifndef __FEATTAG_H__
#define __FEATTAG_H__

#include "AI8G.h"

#define FEATTAG_MIN_LEN 6     /* 输入字符串最小长度 */
#define FEATTAG_MAX_LEN 16    /* 输入字符串最大长度 */
#define FEATTAG_OUT_LEN 16    /* 输出校验码字节数 */

/* FeatTag: 面向8位MCU的紧凑型特征算法(中等安全强度)
 * 输入6~16字节字符串, 输出16字节校验码。
 * 采用"吸收-扩散-挤出"的sponge结构: 16字节状态, 每字节吸收时做
 * 加-循环左移-异或的混淆, 吸收完成后进行固定轮数的非线性扩散,
 * 最后逐字节挤出。代码体积远小于完整密码学哈希, 但优于简单异或。 */
void FeatTag_Checksum(const char *in, uint8_t len, uint8_t *out);

#endif /* __FEATTAG_H__ */
