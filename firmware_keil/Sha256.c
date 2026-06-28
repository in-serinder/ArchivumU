#include "Sha256.h"

static const uint32_t k[64] = {
    0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1,
    0x923f82a4, 0xab1c5ed5, 0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3,
    0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174, 0xe49b69c1, 0xefbe4786,
    0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
    0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147,
    0x06ca6351, 0x14292967, 0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13,
    0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85, 0xa2bfe8a1, 0xa81a664b,
    0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
    0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a,
    0x5b9cca4f, 0x682e6ff3, 0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208,
    0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2};

static uint32_t rotr(uint32_t x, uint8_t n) {
  return (x >> n) | (x << (32 - n));
}

static uint32_t ch(uint32_t x, uint32_t y, uint32_t z) {
  return (x & y) ^ (~x & z);
}

static uint32_t maj(uint32_t x, uint32_t y, uint32_t z) {
  return (x & y) ^ (x & z) ^ (y & z);
}

static uint32_t sigma0(uint32_t x) {
  return rotr(x, 2) ^ rotr(x, 13) ^ rotr(x, 22);
}

static uint32_t sigma1(uint32_t x) {
  return rotr(x, 6) ^ rotr(x, 11) ^ rotr(x, 25);
}

static uint32_t gamma0(uint32_t x) {
  return rotr(x, 7) ^ rotr(x, 18) ^ (x >> 3);
}

static uint32_t gamma1(uint32_t x) {
  return rotr(x, 17) ^ rotr(x, 19) ^ (x >> 10);
}

static void sha256_transform(Sha256Context *ctx,
                             const uint8_t block[SHA256_BLOCK_LEN]) {
  uint32_t w[64];
  uint32_t a, b, c, d, e, f, g, h, t1, t2;
  uint8_t i;

  for (i = 0; i < 16; i++) {
    w[i] = ((uint32_t)block[i * 4] << 24) | ((uint32_t)block[i * 4 + 1] << 16) |
           ((uint32_t)block[i * 4 + 2] << 8) | (uint32_t)block[i * 4 + 3];
  }

  for (i = 16; i < 64; i++) {
    w[i] = gamma1(w[i - 2]) + w[i - 7] + gamma0(w[i - 15]) + w[i - 16];
  }

  a = ctx->state[0];
  b = ctx->state[1];
  c = ctx->state[2];
  d = ctx->state[3];
  e = ctx->state[4];
  f = ctx->state[5];
  g = ctx->state[6];
  h = ctx->state[7];

  for (i = 0; i < 64; i++) {
    t1 = h + sigma1(e) + ch(e, f, g) + k[i] + w[i];
    t2 = sigma0(a) + maj(a, b, c);
    h = g;
    g = f;
    f = e;
    e = d + t1;
    d = c;
    c = b;
    b = a;
    a = t1 + t2;
  }

  ctx->state[0] += a;
  ctx->state[1] += b;
  ctx->state[2] += c;
  ctx->state[3] += d;
  ctx->state[4] += e;
  ctx->state[5] += f;
  ctx->state[6] += g;
  ctx->state[7] += h;
}

void sha256_init(Sha256Context *ctx) {
  ctx->state[0] = 0x6a09e667;
  ctx->state[1] = 0xbb67ae85;
  ctx->state[2] = 0x3c6ef372;
  ctx->state[3] = 0xa54ff53a;
  ctx->state[4] = 0x510e527f;
  ctx->state[5] = 0x9b05688c;
  ctx->state[6] = 0x1f83d9ab;
  ctx->state[7] = 0x5be0cd19;
  ctx->bit_len = 0;
}

void sha256_update(Sha256Context *ctx, const uint8_t *data, uint32_t len) {
  uint32_t i;
  uint32_t buffer_pos = (uint32_t)(ctx->bit_len >> 3) % SHA256_BLOCK_LEN;

  ctx->bit_len += (uint64_t)len << 3;

  for (i = 0; i < len; i++) {
    ctx->buffer[buffer_pos++] = data[i];
    if (buffer_pos == SHA256_BLOCK_LEN) {
      sha256_transform(ctx, ctx->buffer);
      buffer_pos = 0;
    }
  }
}

void sha256_final(Sha256Context *ctx, uint8_t digest[SHA256_DIGEST_LEN]) {
  uint32_t buffer_pos = (uint32_t)(ctx->bit_len >> 3) % SHA256_BLOCK_LEN;
  uint8_t i;

  ctx->buffer[buffer_pos++] = 0x80;

  if (buffer_pos > 56) {
    while (buffer_pos < SHA256_BLOCK_LEN) {
      ctx->buffer[buffer_pos++] = 0;
    }
    sha256_transform(ctx, ctx->buffer);
    buffer_pos = 0;
  }

  while (buffer_pos < 56) {
    ctx->buffer[buffer_pos++] = 0;
  }

  ctx->buffer[56] = (uint8_t)(ctx->bit_len >> 56);
  ctx->buffer[57] = (uint8_t)(ctx->bit_len >> 48);
  ctx->buffer[58] = (uint8_t)(ctx->bit_len >> 40);
  ctx->buffer[59] = (uint8_t)(ctx->bit_len >> 32);
  ctx->buffer[60] = (uint8_t)(ctx->bit_len >> 24);
  ctx->buffer[61] = (uint8_t)(ctx->bit_len >> 16);
  ctx->buffer[62] = (uint8_t)(ctx->bit_len >> 8);
  ctx->buffer[63] = (uint8_t)ctx->bit_len;

  sha256_transform(ctx, ctx->buffer);

  for (i = 0; i < 8; i++) {
    digest[i * 4] = (uint8_t)(ctx->state[i] >> 24);
    digest[i * 4 + 1] = (uint8_t)(ctx->state[i] >> 16);
    digest[i * 4 + 2] = (uint8_t)(ctx->state[i] >> 8);
    digest[i * 4 + 3] = (uint8_t)ctx->state[i];
  }
}

void sha256_hash(const uint8_t *data, uint32_t len,
                 uint8_t digest[SHA256_DIGEST_LEN]) {
  Sha256Context ctx;
  sha256_init(&ctx);
  sha256_update(&ctx, data, len);
  sha256_final(&ctx, digest);
}

void sha256_with_slat(const uint8_t sha256_in[SHA256_DIGEST_LEN],
                      uint8_t result[SHA256_DIGEST_LEN]) {
  uint8_t temp[SHA256_DIGEST_LEN + 9];
  uint8_t i;
  for (i = 0; i < SHA256_DIGEST_LEN; i++) {
    temp[i] = sha256_in[i];
  }
  for (i = 0; i < 9; i++) {
    temp[SHA256_DIGEST_LEN + i] = SLAT[i];
  }
  sha256_hash(temp, SHA256_DIGEST_LEN + 9, result);
}