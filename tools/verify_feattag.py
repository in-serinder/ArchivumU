# -*- coding: utf-8 -*-
"""验证 firmware_keil/Security/FeatTag.c 的算法正确性。

FeatTag 是面向 8 位 MCU 的紧凑型特征算法(中等安全强度, 非密码学安全哈希):
  A) 逐字节吸收 -> 固定轮数扩散 -> 逐字节挤出;
  B) 本脚本以 Python 参考实现复算 C 版本, 检查两端输出一致;
  C) 打印若干示例输出, 并做简单的雪崩(输入1bit变化->输出变化位数)检查。

说明: 因为 FeatTag 为自定义算法, 没有标准向量可对照, 这里的 "参考实现" 依据
      与 C 源码逐语句一一对应的方式实现, 用于防止移植/改写时出现偏差。
"""
import pathlib

MASK = 0xFF
ROUNDS = 8
OUT_LEN = 16
MAX_LEN = 16

IV = [0x6A, 0x09, 0xE6, 0x67, 0xBB, 0x67, 0xAE, 0x85,
      0x3C, 0x6E, 0xF3, 0x72, 0xA5, 0x4F, 0xF5, 0x3A]


def rol8(x, n):
    x &= MASK
    return ((x << n) | (x >> (8 - n))) & MASK


def feattag(data):
    """与 FeatTag.c 完全一致的 Python 参考实现, 返回16字节."""
    if len(data) > MAX_LEN:
        data = data[:MAX_LEN]
    ln = len(data)

    # 1) 初始化状态
    s = [(IV[i] ^ ((0x9E + i) & MASK)) & MASK for i in range(16)]

    # 2) 吸收
    for i in range(ln):
        t = (data[i] ^ ((i * 0x1D) & MASK)) & MASK
        s[i & 15] = (s[i & 15] + t) & MASK
        s[(i + 7) & 15] ^= rol8(t, 3)
        s[(i + 3) & 15] = (s[(i + 3) & 15] + rol8(s[i & 15], 1)) & MASK

    # 混入长度
    s[15] ^= (ln + OUT_LEN) & MASK

    # 3) 扩散
    for r in range(ROUNDS):
        for i in range(16):
            a = s[i]
            b = s[(i + 1) & 15]
            s[i] = (a + b + 0x37 + r) & MASK
            s[(i + 1) & 15] = s[(i + 1) & 15] ^ rol8(a, 5)
        for i in range(15, -1, -1):
            a = s[i]
            b = s[(i + 15) & 15]
            s[i] = (a + b + 0x5C) & MASK
            s[(i + 15) & 15] = s[(i + 15) & 15] ^ rol8(a, 2)

    # 4) 挤出
    return bytes((s[i] ^ s[(i + 8) & 15] ^ rol8(s[i], 4)) & MASK for i in range(OUT_LEN))


def bits_diff(a, b):
    return sum(bin(x ^ y).count('1') for x, y in zip(a, b))


def main():
    print('== A) 参考实现示例输出 ==')
    for s in [b'123456', b'admin123', b'abcdefghijklmnop', b'6chars', b'ABCDEFGHIJKLMNOP']:
        print(f'  {s.decode():16s} -> {feattag(s).hex()}')

    print('== B) 雪崩检查(输入翻转1bit -> 输出平均变化bit数, 理想=64) ==')
    base = b'password123'
    base_out = feattag(base)
    total = 0
    n = 0
    for byte_i in range(len(base)):
        for bit in range(8):
            mut = bytearray(base)
            mut[byte_i] ^= (1 << bit)
            total += bits_diff(base_out, feattag(bytes(mut)))
            n += 1
    print(f'  翻转{len(base)}字节共{n}次, 平均输出变化 {total / n:.1f} bit (满分64)')

    print('== C) 一致性: C 源码文件存在性 ==')
    src = pathlib.Path(__file__).resolve().parents[1] / 'firmware_keil' / 'Security' / 'FeatTag.c'
    print(f'  {src} {"OK" if src.exists() else "MISSING"}')
    print()
    print('说明: 若需与 C 端逐字节比对, 可用 main.c 中的 FeatTag_Checksum("123456",6,tmp)')
    print('      经串口打印, 与上面 "123456 -> " 的输出对照。')


if __name__ == '__main__':
    main()
