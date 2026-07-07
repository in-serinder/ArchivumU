using System;
using ArchivumU.ViewModels;

namespace ArchivumU.Models
{
    public static class AT_CMDTXHelperModel
    {
        private static string Quote(string value)
        {
            return $"\"{value}\"";
        }

        public static string AT_INIT(string deviceName, bool enablePassword = false, string password = "",DevInitViewModelItem.EncryptionType encryptionType = DevInitViewModelItem.EncryptionType.NoneEncryption)
        {
            // 加密方式(0-NON，1-AES，2-XOR,3-CESAR，4-RC4)
            int encryptType = 0;
            switch (encryptionType)
            {
                case DevInitViewModelItem.EncryptionType.NoneEncryption:
                    encryptType = 0;
                    break;
                case DevInitViewModelItem.EncryptionType.AES128:
                    encryptType = 1;
                    break;
                case DevInitViewModelItem.EncryptionType.XOR:
                    encryptType = 2;
                    break;
                case DevInitViewModelItem.EncryptionType.Caesar:
                    encryptType = 3;
                    break;
                case DevInitViewModelItem.EncryptionType.RC4:
                    encryptType = 4;
                    break;
                default:
                    encryptType = 0;
                    break;
            }
            return $"AT+INIT+{Quote(deviceName)}+{Quote(enablePassword?password:"UPASS")}+{encryptType}";
        }

        public static string AT_ECHO()
        {
            return "AT+ECHO";
        }

        public static string AT_INFO()
        {
            return "AT+INFO";
        }

        public static string AT_STATUS()
        {
            return "AT+STATUS";
        }

        public static string AT_AUTH_PASSWORD_CREATE(string password)
        {
            return $"AT+AUTH+PASSWORD+CREATE+{Quote(password)}";
        }

        public static string AT_AUTH_PASSWORD_VERIFY(string password)
        {
            return $"AT+AUTH+PASSWORD+VERIFY+{Quote(password)}";
        }

        public static string AT_AUTH_PASSWORD_ENABLE()
        {
            return "AT+AUTH+PASSWORD+ENABLE";
        }

        public static string AT_AUTH_PASSWORD_DISABLE()
        {
            return "AT+AUTH+PASSWORD+DISABLE";
        }

        public static string AT_AUTH_PASSWORD_VERIFYOUT()
        {
            return "AT+AUTH+PASSWORD+VERIFYOUT";
        }

        public static string AT_READ_BLOCK(int blockId)
        {
            return $"AT+READ+BLOCK+{blockId}";
        }

        public static string AT_READ_KEY(int blockId, string key)
        {
            return $"AT+READ+KEY+{blockId}+{Quote(key)}";
        }

        public static string AT_WRITE(int blockId, string key, string value)
        {
            return $"AT+WRITE+{blockId}+{Quote(key)}+{Quote(value)}";
        }

        public static string AT_CREATE_BLOCK(string blockName, int blockSize = 16)
        {
            return $"AT+CREATE+BLOCK+{Quote(blockName)}+{blockSize}";
        }

        public static string AT_CREATE_KEY(int blockFlag, string blockIdOrName, string key, string value)
        {
            return $"AT+CREATE+KEY+{blockFlag}+{Quote(blockIdOrName)}+{Quote(key)}+{Quote(value)}";
        }

        public static string AT_DELETE_BLOCK(int blockId)
        {
            return $"AT+DELETE+BLOCK+{blockId}";
        }

        public static string AT_DELETE_KEY(int blockFlag, string blockIdOrName, string key, string value)
        {
            return $"AT+DELETE+KEY+{blockFlag}+{Quote(blockIdOrName)}+{Quote(key)}+{Quote(value)}";
        }

        public static string AT_UPDATE_BLOCK(int blockId)
        {
            return $"AT+UPDATE+BLOCK+{blockId}";
        }

        public static string AT_UPDATE_KEY(int blockId, string key, string newValue)
        {
            return $"AT+UPDATE+KEY+{blockId}+{Quote(key)}+{Quote(newValue)}";
        }

        public static string AT_GET_ALL_BLOCK()
        {
            return "AT+GET+ALL+BLOCK";
        }

        public static string AT_FORMAT_DEV()
        {
            return "AT+FORMAT+DEV";
        }

        public static string AT_FORMAT_BLOCK(int blockFlag, string blockIdOrName)
        {
            return $"AT+FORMAT+BLOCK+{blockFlag}+{Quote(blockIdOrName)}";
        }
    }
}