namespace ArchivumU.Models;

public class TypeModel
{
    public enum EncryptionType
    {
        NoneEncryption,
        AES128,
        XOR,
        Caesar,
        RC4
    }

}

/// <summary>
/// 加密算法枚举（与固件 StorageMgr.h 的 ENC_ALGO_* 一致，见 CMD.md 4.1）。
/// </summary>
public enum EncryptionAlgorithm
{
    None = 0,
    Aes128Cbc = 1,
    Aes128Gcm = 2,
    Xor = 3,
    Caesar = 4,
    Rc4 = 5
}

/// <summary>
/// 数据片容量代码（与固件 DATA_SIZE_* 一致，见 CMD.md 4.2）。
/// </summary>
public enum DataSizeCode
{
    None = 0,
    Size24C32 = 1,
    Size24C64 = 2,
    Size24C128 = 3,
    Size24C256 = 4,
    Size24C512 = 5
}
