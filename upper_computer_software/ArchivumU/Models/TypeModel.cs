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