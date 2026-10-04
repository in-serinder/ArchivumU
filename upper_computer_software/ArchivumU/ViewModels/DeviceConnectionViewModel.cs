using System;
using System.Threading.Tasks;
using ArchivumU.Models;

namespace ArchivumU.Services;

/// <summary>
/// 设备连接业务服务：封装「双击设备 -> 读取信息 -> 认证/初始化/直连 -> 同步配置」的完整流程。
///
/// 约定：
///  - 一切以「端口号」为主键。上位机若已存在该端口的记录，则以下位机（设备）返回的信息覆盖之。
///  - 设备未初始化 -&gt; 交由调用方弹出初始化窗口（DevInitWindow）。
///  - 设备已初始化且需要密码 -&gt; 交由调用方弹出认证窗口（AuthWindow）。
///  - 设备已初始化且无需密码 -&gt; 直接进入连接态。
/// </summary>
public static class DeviceConnectionService
{
    /// <summary>连接环节的判定结果。</summary>
    public enum ConnectionState
    {
        /// <summary>不是 ArchivumU 设备或串口被占用。</summary>
        NotArchivumU,
        /// <summary>设备未初始化，需要初始化。</summary>
        NeedInit,
        /// <summary>设备已初始化但需要密码认证。</summary>
        NeedAuth,
        /// <summary>设备已初始化且无需密码，可直接连接。</summary>
        Ready
    }

    /// <summary>连接判定结果。</summary>
    public sealed class ConnectionProbe
    {
        /// <summary>判定状态。</summary>
        public ConnectionState State { get; init; }

        /// <summary>端口号。</summary>
        public string PortName { get; init; } = string.Empty;

        /// <summary>设备信息（可能为 null）。</summary>
        public DevBaseInfo? Info { get; init; }

        /// <summary>是否探测成功（能拿到有效回包）。</summary>
        public bool Success => Info != null && State != ConnectionState.NotArchivumU;

        /// <summary>描述信息。</summary>
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// 探测指定端口上的设备：读取 INFO 并判定后续流程。
    /// </summary>
    public static async Task<ConnectionProbe> ProbeAsync(string portName, int timeoutMs = 3000)
    {
        var helper = new SerialObjectHelperModel();

        // 预执行一次，清理可能滞后的缓冲区
        await helper.QuickSendCommand(portName, AT_CMDTXHelperModel.AT_INFO(), timeoutMs);

        var (ret, response) =
            await helper.QuickSendCommand(portName, AT_CMDTXHelperModel.AT_INFO(), timeoutMs);

        if (!ret)
        {
            return new ConnectionProbe
            {
                PortName = portName,
                State = ConnectionState.NotArchivumU,
                Message = response
            };
        }

        DevBaseInfo? info = AT_CMDRXPeaserHelperModel.ParseInfoResponse(response);
        if (info == null)
        {
            return new ConnectionProbe
            {
                PortName = portName,
                State = ConnectionState.NotArchivumU,
                Info = null,
                Message = "Not an ArchivumU device"
            };
        }

        // 未初始化：DeviceName 为 null（回包为 INIT=0+版本）
        if (string.IsNullOrEmpty(info.DeviceName))
        {
            return new ConnectionProbe
            {
                PortName = portName,
                State = ConnectionState.NeedInit,
                Info = info,
                Message = "Device not initialized"
            };
        }

        // 已初始化且需要密码
        if (info.PasswordStatus)
        {
            return new ConnectionProbe
            {
                PortName = portName,
                State = ConnectionState.NeedAuth,
                Info = info,
                Message = "Authentication required"
            };
        }

        // 已初始化且无需密码
        return new ConnectionProbe
        {
            PortName = portName,
            State = ConnectionState.Ready,
            Info = info,
            Message = "Ready"
        };
    }

    /// <summary>
    /// 校验密码是否通过（发送 AUTH+PASSWORD+VERIFY）。
    /// </summary>
    public static async Task<bool> VerifyPasswordAsync(string portName, string password, int timeoutMs = 3000)
    {
        var helper = new SerialObjectHelperModel();
        var (ret, response) = await helper.QuickSendCommand(
            portName,
            AT_CMDTXHelperModel.AT_AUTH_PASSWORD_VERIFY(password),
            timeoutMs);

        if (!ret || string.IsNullOrEmpty(response))
        {
            return false;
        }

        // 兼容 AUTH+0 / RESULT+0 / DATA+OK 等成功回包
        string trimmed = response.Trim();
        return trimmed.Contains("AUTH+0")
               || trimmed.Contains("RESULT+0")
               || trimmed.Contains("DATA+OK")
               || trimmed.Contains("OK");
    }

    /// <summary>
    /// 将设备信息同步到上位机配置（以端口号为主键；下位机信息覆盖上位机已有记录）。
    /// 返回同步后的设备配置。
    /// </summary>
    public static DeviceConfig SyncDeviceConfig(DevBaseInfo info, string portName)
    {
        var service = ConfigJsonService.Instance;
        var existing = service.GetDeviceByPort(portName);

        var record = new DeviceConfig
        {
            Name = string.IsNullOrEmpty(info.DeviceName) ? (existing?.Name ?? portName) : info.DeviceName,
            Port = portName,
            EncryptionMode = string.IsNullOrEmpty(info.EncryptionType) ? "NON" : info.EncryptionType,
            Storage = new StorageInfo
            {
                TotalSize = info.TotalSize,
                UsedSize = existing?.Storage?.UsedSize ?? 0
            }
        };

        if (existing != null)
        {
            // 下位机信息覆盖上位机已有记录（按端口主键）
            service.UpdateDevice(existing.Name, record);
        }
        else
        {
            service.AddDevice(record);
        }

        return record;
    }
}
