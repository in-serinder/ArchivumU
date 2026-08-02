#include "Auth.h"


const  char xdata slat[16] = "ArchivumU1234567";
char idata auth_key[32] = {0};
char * Auth_GenerateKey(char *password) {
  //盐+密码
	uint8_t i;
  BLAKE2s_Checksum(password, 16, auth_key);
  for ( i = 15; i < 32; i++) {
    auth_key[i] = auth_key[i] | slat[i];
  }
  return auth_key;
}