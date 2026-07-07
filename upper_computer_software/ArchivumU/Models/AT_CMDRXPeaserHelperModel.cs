namespace ArchivumU.Models;

public class AT_CMDRXPeaserHelperModel
{
    public static DevBaseInfo ParseInfoResponse(string response)
    {
        if (string.IsNullOrEmpty(response))
            return null;

        var parts = response.Split('+');
    
        // 情况1：设备已初始化 - INFO+设备名+固件版本+密码状态+接入计数+块数量+键值对数量
        if (parts.Length >= 7 && parts[0] == "INFO")
        {
            // INFO+ArchivumU+V1.0.0+DISABLED+0+0+0+CESAR+16384
            try
            {
                return new DevBaseInfo
                {
                    DeviceName = parts[1],
                    FirmwareVersion = parts[2],
                    PasswordStatus = parts[3] == "ENABLED",
                    AccessCount = int.TryParse(parts[4], out int access) ? access : 0,
                    BlockCount = int.TryParse(parts[5], out int blocks) ? blocks : 0,
                    KeyValueCount = int.TryParse(parts[6], out int kv) ? kv : 0,
                    EncryptionType = parts[7],
                    TotalSize = long.TryParse(parts[8], out long size) ? size : 0
                };
            }
            catch
            {
                return null;
            }
        }
    
        // 情况2：设备未初始化 - INIT=0+固件版本
        if (parts.Length >= 2 && parts[0] == "INIT=0")
        {
            return new DevBaseInfo
            {
                DeviceName = null,
                FirmwareVersion = parts[1],
                PasswordStatus = false,
                AccessCount = 0,
                BlockCount = 0,
                KeyValueCount = 0
            };
        }
    
        return null;
    }
}

/// <summary>
/// 设备基本信息信息 类
/// </summary>
public class DevBaseInfo
{
    public string DeviceName { get; set; }
    public string FirmwareVersion { get; set; }
    public bool PasswordStatus { get; set; }
    public int AccessCount { get; set; }
    public int BlockCount { get; set; }
    public int KeyValueCount { get; set; }
    public long TotalSize { get; set; }
    public string EncryptionType { get; set; }
}
