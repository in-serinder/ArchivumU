#ifndef __SHA256_H__
#define __SHA256_H__
#include "AI8G.h"

#define SHA256_DIGEST_LEN 32
#define SHA256_BLOCK_LEN 64

#define SLAT "ArchivumU"

typedef struct {
  uint32_t state[8];
  uint64_t bit_len;
  uint8_t buffer[SHA256_BLOCK_LEN];
} Sha256Context;

void sha256_init(Sha256Context *ctx);
void sha256_update(Sha256Context *ctx, const uint8_t *data, uint32_t len);
void sha256_final(Sha256Context *ctx, uint8_t digest[SHA256_DIGEST_LEN]);
void sha256_hash(const uint8_t *data, uint32_t len,
                 uint8_t digest[SHA256_DIGEST_LEN]);
void sha256_with_slat(const uint8_t sha256_in[SHA256_DIGEST_LEN],
                      uint8_t result[SHA256_DIGEST_LEN]);

#endif // __SHA256_H__