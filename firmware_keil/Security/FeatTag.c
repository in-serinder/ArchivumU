#include "FeatTag.h"

/* FeatTag —— 中等安全强度的紧凑特征算法
 * ------------------------------------------------------------------
 * 设计目标: 在8051(8位, 代码区受限)上以极小代码量提供"优于简单异或"
 *           的口令/配置校验能力。非密码学安全哈希, 但对短口令的抗碰撞、
 *           抗篡改能力明显强于普通校验和。
 *
 * 结构:
 *   1) 状态 s[16] 由固定IV初始化;
 *   2) 吸收(absorb): 逐字节把输入混入状态, 位置相关, 含加/循环左移/异或;
 *   3) 扩散(diffuse): 固定ROUNDS轮的非线性混淆, 每轮更新全部16字节;
 *   4) 挤出(squeeze): 逐字节把状态扩散输出, 共16字节。
 *
 * 代码量约 < 1.2KB。
 * ------------------------------------------------------------------ */

#define FEATTAG_ROUNDS 8   /* 扩散轮数, 越大越安全, 代码量不变仅时间增加 */

/* 16字节工作状态 */
static uint8_t idata ft_s[16];

/* 固定初值(IV) */
static const uint8_t code ft_iv[16] = {
  0x6A, 0x09, 0xE6, 0x67, 0xBB, 0x67, 0xAE, 0x85,
  0x3C, 0x6E, 0xF3, 0x72, 0xA5, 0x4F, 0xF5, 0x3A
};

/* 8位循环左移 */
#define ROL8(x, n) ((uint8_t)(((uint8_t)(x) << (n)) | ((uint8_t)(x) >> (8 - (n)))))

void FeatTag_Checksum(const char *in, uint8_t len, uint8_t *out)
{
  uint8_t i, r, t, a, b;

  if (len > FEATTAG_MAX_LEN)
    len = FEATTAG_MAX_LEN;

  /* 1) 初始化状态 */
  for (i = 0; i < 16; i++)
    ft_s[i] = (uint8_t)(ft_iv[i] ^ (uint8_t)(0x9E + i));

  /* 2) 吸收输入: 每字节混入状态, 并做局部混淆 */
  for (i = 0; i < len; i++) {
    t = (uint8_t)in[i] ^ (uint8_t)(i * 0x1D);
    ft_s[i & 15]        = (uint8_t)(ft_s[i & 15] + t);
    ft_s[(i + 7) & 15] ^= ROL8(t, 3);
    ft_s[(i + 3) & 15]  = (uint8_t)(ft_s[(i + 3) & 15] + ROL8(ft_s[i & 15], 1));
  }

  /* 混入长度, 防止长度扩展/填充歧义 */
  ft_s[15] ^= (uint8_t)(len + FEATTAG_OUT_LEN);

  /* 3) 扩散: 固定轮数的非线性混淆, 每轮前向+后向各扫一遍 */
  for (r = 0; r < FEATTAG_ROUNDS; r++) {
    /* 前向 */
    for (i = 0; i < 16; i++) {
      a = ft_s[i];
      b = ft_s[(i + 1) & 15];
      ft_s[i]            = (uint8_t)(a + b + 0x37 + r);
      ft_s[(i + 1) & 15] = (uint8_t)(ft_s[(i + 1) & 15] ^ ROL8(a, 5));
    }
    /* 后向 */
    for (i = 16; i-- > 0; ) {
      a = ft_s[i];
      b = ft_s[(i + 15) & 15];        /* 前一个字节(i-1) */
      ft_s[i]            = (uint8_t)(a + b + 0x5C);
      ft_s[(i + 15) & 15] = (uint8_t)(ft_s[(i + 15) & 15] ^ ROL8(a, 2));
    }
  }

  /* 4) 挤出输出: 状态对折异或再循环左移, 保证雪崩 */
  for (i = 0; i < FEATTAG_OUT_LEN; i++) {
    a = ft_s[i];
    b = ft_s[(i + 8) & 15];
    out[i] = (uint8_t)(a ^ b ^ ROL8(a, 4));
  }
}
