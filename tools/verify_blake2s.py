# -*- coding: utf-8 -*-
"""验证 firmware_keil/Security/BLAKE2s.c 的算法与展开正确性。

A) 用标准的 sigma 表实现 BLAKE2s-256, 与 hashlib.blake2s(digest_size=32) 对比。
B) 从 BLAKE2s.c 源码中提取 R0..R9 的 GX(消息索引对), 与标准 sigma 表逐轮对比。
"""
import hashlib, re, pathlib

IV = [0x6A09E667, 0xBB67AE85, 0x3C6EF372, 0xA54FF53A,
      0x510E527F, 0x9B05688C, 0x1F83D9AB, 0x5BE0CD19]
SIGMA = [
    [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15],
    [14, 10, 4, 8, 9, 15, 13, 6, 1, 12, 0, 2, 11, 7, 5, 3],
    [11, 8, 12, 0, 5, 2, 15, 13, 10, 14, 3, 6, 7, 1, 9, 4],
    [7, 9, 3, 1, 13, 12, 11, 14, 2, 6, 5, 10, 4, 0, 15, 8],
    [9, 0, 5, 7, 2, 4, 10, 15, 14, 1, 11, 12, 6, 8, 3, 13],
    [2, 12, 6, 10, 0, 11, 8, 3, 4, 13, 7, 5, 15, 14, 1, 9],
    [12, 5, 1, 15, 14, 13, 4, 10, 0, 7, 6, 3, 9, 2, 8, 11],
    [13, 11, 7, 14, 12, 1, 3, 9, 5, 0, 15, 4, 8, 6, 2, 10],
    [6, 15, 14, 9, 11, 3, 0, 8, 12, 2, 13, 7, 1, 4, 10, 5],
    [10, 2, 8, 4, 7, 6, 1, 5, 15, 11, 9, 14, 3, 12, 13, 0],
]
MASK = 0xFFFFFFFF
STATE_INDICES = {
    'column': [(0, 4, 8, 12), (1, 5, 9, 13), (2, 6, 10, 14), (3, 7, 11, 15)],
    'diag':   [(0, 5, 10, 15), (1, 6, 11, 12), (2, 7, 8, 13), (3, 4, 9, 14)],
}


def ror(x, n):
    return ((x >> n) | (x << (32 - n))) & MASK


def G(v, a, b, c, d, x, y):
    v[a] = (v[a] + v[b] + x) & MASK
    v[d] = ror(v[d] ^ v[a], 16)
    v[c] = (v[c] + v[d]) & MASK
    v[b] = ror(v[b] ^ v[c], 12)
    v[a] = (v[a] + v[b] + y) & MASK
    v[d] = ror(v[d] ^ v[a], 8)
    v[c] = (v[c] + v[d]) & MASK
    v[b] = ror(v[b] ^ v[c], 7)


def blake2s256(data):
    """标准 BLAKE2s-256 (单块, 参考实现结构), 用于与 hashlib 对照."""
    h = IV[:]
    h[0] ^= 0x01010020                      # kk=0, nn=32
    m = [0] * 16
    for i, b in enumerate(data):
        m[i >> 2] |= b << ((i & 3) * 8)
    v = h + IV[:]
    v[12] ^= len(data)                      # t0 = len
    v[14] ^= 0xFFFFFFFF                     # f0 = 1 (末块)
    for r in range(10):
        s = SIGMA[r]
        for (a, b, c, d), (p, q) in zip(STATE_INDICES['column'], [(s[0], s[1]), (s[2], s[3]), (s[4], s[5]), (s[6], s[7])]):
            G(v, a, b, c, d, m[p], m[q])
        for (a, b, c, d), (p, q) in zip(STATE_INDICES['diag'], [(s[8], s[9]), (s[10], s[11]), (s[12], s[13]), (s[14], s[15])]):
            G(v, a, b, c, d, m[p], m[q])
    # 参考实现: h[i] = h[i] ^ v[i] ^ v[i+8] (h为压缩前状态)
    return b''.join(((h[i] ^ v[i] ^ v[i + 8]) & MASK).to_bytes(4, 'little') for i in range(8))


def c_impl(data, fold_op):
    """与 BLAKE2s.c 完全一致的单块实现(含就地合成与常数折半), 返回16字节."""
    m = [0] * 16
    for i, b in enumerate(data):
        m[i >> 2] |= b << ((i & 3) * 8)
    v = [0] * 16
    v[0] = 0x6A09E667 ^ 0x01010020
    v[1] = 0xBB67AE85; v[2] = 0x3C6EF372; v[3] = 0xA54FF53A
    v[4] = 0x510E527F; v[5] = 0x9B05688C; v[6] = 0x1F83D9AB; v[7] = 0x5BE0CD19
    v[8] = 0x6A09E667; v[9] = 0xBB67AE85; v[10] = 0x3C6EF372; v[11] = 0xA54FF53A
    v[12] = 0x510E527F ^ len(data)
    v[13] = 0x9B05688C
    v[14] = 0x1F83D9AB ^ 0xFFFFFFFF
    v[15] = 0x5BE0CD19
    for r in range(10):
        s = SIGMA[r]
        for (a, b, c, d), (p, q) in zip(STATE_INDICES['column'], [(s[0], s[1]), (s[2], s[3]), (s[4], s[5]), (s[6], s[7])]):
            G(v, a, b, c, d, m[p], m[q])
        for (a, b, c, d), (p, q) in zip(STATE_INDICES['diag'], [(s[8], s[9]), (s[10], s[11]), (s[12], s[13]), (s[14], s[15])]):
            G(v, a, b, c, d, m[p], m[q])
    for i in range(8):
        v[i] ^= v[i + 8]
    F = {0: lambda a, b: a | b, 1: lambda a, b: a ^ b, 2: lambda a, b: a & b}[fold_op]
    out = bytearray(16)
    for o, (a, b) in enumerate([(0x6B08E647, 0x510E527F), (0xBB67AE85, 0x9B05688C),
                                (0x3C6EF372, 0x1F83D9AB), (0xA54FF53A, 0x5BE0CD19)]):
        t = F(a ^ v[o], b ^ v[o + 4])
        out[o * 4:o * 4 + 4] = t.to_bytes(4, 'little')
    return bytes(out)


FOLD_OPS = {0: 'OR', 1: 'XOR', 2: 'AND'}


def fold32to16(dig32, op):
    dig = bytearray(dig32)
    for i in range(16):
        if op == 0:
            dig[i] |= dig[i + 16]
        elif op == 2:
            dig[i] &= dig[i + 16]
        else:
            dig[i] ^= dig[i + 16]
    return bytes(dig[:16])


def main():
    print('== A) 32字节摘要 对照 hashlib ==')
    tests = [b'', b'abc', b'123456', b'admin123', b'abcdefghijklmnop',
             b'password123456', bytes(range(16)), '密码测试'.encode('utf-8')]
    ok = True
    for t in tests:
        mine = blake2s256(t)
        ref = hashlib.blake2s(t, digest_size=32).digest()
        match = mine == ref
        ok &= match
        print(f'  len={len(t):2d} {"OK " if match else "BAD"} mine={mine.hex()} ref={ref.hex()}')

    print('== B) BLAKE2s.c 展开轮次与 sigma 表对照 ==')
    src = pathlib.Path(__file__).resolve().parents[1] / 'firmware_keil' / 'Security' / 'BLAKE2s.c'
    text = src.read_text(encoding='utf-8')
    rounds = re.findall(r'#define (RD\d+) \\\n(.*?)(?=\n#define RD\d|\n\nvoid BLAKE2s)', text, re.S)
    ok2 = True
    for name, body in rounds:
        gxs = re.findall(r'GX\((\d+),(\d+),(\d+),(\d+),(\d+),(\d+)\)', body)
        pairs = [(int(g[4]), int(g[5])) for g in gxs]
        r = int(name[2:])
        s = SIGMA[r]
        expect = [(s[0], s[1]), (s[2], s[3]), (s[4], s[5]), (s[6], s[7]),
                  (s[8], s[9]), (s[10], s[11]), (s[12], s[13]), (s[14], s[15])]
        match = pairs == expect
        ok2 &= match
        print(f'  {name}: {"OK " if match else "BAD"} pairs={pairs}')
        if not match:
            print(f'        expect={expect}')

    print('== C) BLAKE2s.c 端到端输出(XOR折半) 对照 hashlib+折半 ==')
    ok3 = True
    for pw in [b'123456', b'admin123', b'abcdefghijklmnop', b'6chars', b'ABCDEFGHIJKLMNOP']:
        mine = c_impl(pw, 1)
        ref = fold32to16(hashlib.blake2s(pw, digest_size=32).digest(), 1)
        match = mine == ref
        ok3 &= match
        print(f'  {pw.decode():16s} {"OK " if match else "BAD"} -> {mine.hex()}')

    print('== D) 三种折半运算的16字节示例 ==')
    for op in (0, 1, 2):
        print(f'  {FOLD_OPS[op]:3s} 123456    -> {fold32to16(hashlib.blake2s(b"123456", digest_size=32).digest(), op).hex()}')

    print()
    print('RESULT:', 'ALL OK' if (ok and ok2 and ok3) else 'MISMATCH FOUND')


if __name__ == '__main__':
    main()
