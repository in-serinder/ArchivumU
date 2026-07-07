using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArchivumU.ViewModels;

namespace ArchivumU.Services;

public class ConfigJsonService
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public static ConfigJsonService Instance { get; } = new ConfigJsonService();

    // 设备列表配置文件名
    private readonly string _deviceListFilePath;

    public ConfigJsonService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "ArchivumU");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "configs.json");
        _deviceListFilePath = Path.Combine(dir, "device_list.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true
        };
    }

    #region 底层读写（原有）
    public List<ConfigItem> LoadAll()
    {
        if (!File.Exists(_filePath))
            return new List<ConfigItem>();

        string json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<ConfigItem>>(json, _jsonOptions) ?? new List<ConfigItem>();
    }

    private void SaveAll(List<ConfigItem> list)
    {
        string json = JsonSerializer.Serialize(list, _jsonOptions);
        File.WriteAllText(_filePath, json);
    }
    #endregion

    #region CURD 操作（原有）
    public bool Create(ConfigItem item)
    {
        var list = LoadAll();
        bool exists = list.Any(x => x.ConfigItemName == item.ConfigItemName);
        if (exists) return false;

        list.Add(item);
        SaveAll(list);
        return true;
    }

    public ConfigItem? Read(string itemName)
    {
        var list = LoadAll();
        return list.FirstOrDefault(x => x.ConfigItemName == itemName);
    }

    public bool Update(string itemName, string newValue)
    {
        var list = LoadAll();
        var target = list.FirstOrDefault(x => x.ConfigItemName == itemName);
        if (target == null) return false;

        target.ConfigItemValue = newValue;
        SaveAll(list);
        return true;
    }

    public bool Delete(string itemName)
    {
        var list = LoadAll();
        var target = list.FirstOrDefault(x => x.ConfigItemName == itemName);
        if (target == null) return false;

        list.Remove(target);
        SaveAll(list);
        return true;
    }

    public void Upsert(string itemName, string itemValue)
    {
        var list = LoadAll();
        var target = list.FirstOrDefault(x => x.ConfigItemName == itemName);
        if (target == null)
        {
            list.Add(new ConfigItem(itemName, itemValue));
        }
        else
        {
            target.ConfigItemValue = itemValue;
        }
        SaveAll(list);
    }
    #endregion

    #region 设备列表操作（新增）
    /// <summary>加载设备列表配置</summary>
    private SaveDeviceListConfig LoadDeviceList()
    {
        if (!File.Exists(_deviceListFilePath))
            return new SaveDeviceListConfig();

        try
        {
            string json = File.ReadAllText(_deviceListFilePath);
            return JsonSerializer.Deserialize<SaveDeviceListConfig>(json, _jsonOptions) ?? new SaveDeviceListConfig();
        }
        catch
        {
            return new SaveDeviceListConfig();
        }
    }

    /// <summary>保存设备列表配置</summary>
    private void SaveDeviceList(SaveDeviceListConfig config)
    {
        try
        {
            string json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(_deviceListFilePath, json);
        }
        catch { }
    }

    /// <summary>添加设备配置</summary>
    public bool AddDevice(DeviceConfig device)
    {
        if (string.IsNullOrEmpty(device.Name) || string.IsNullOrEmpty(device.Port))
            return false;

        var config = LoadDeviceList();
        if (config.SaveDeviceList.Any(d => d.Name == device.Name || d.Port == device.Port))
            return false;

        config.SaveDeviceList.Add(device);
        SaveDeviceList(config);
        return true;
    }

    /// <summary>更新设备配置</summary>
    public bool UpdateDevice(string deviceName, DeviceConfig newDevice)
    {
        var config = LoadDeviceList();
        int index = config.SaveDeviceList.FindIndex(d => d.Name == deviceName);
        if (index == -1)
            return false;

        config.SaveDeviceList[index] = newDevice;
        SaveDeviceList(config);
        return true;
    }

    /// <summary>删除设备配置</summary>
    public bool DeleteDevice(string deviceName)
    {
        var config = LoadDeviceList();
        var device = config.SaveDeviceList.FirstOrDefault(d => d.Name == deviceName);
        if (device == null)
            return false;

        config.SaveDeviceList.Remove(device);
        SaveDeviceList(config);
        return true;
    }

    /// <summary>根据名称获取设备配置</summary>
    public DeviceConfig? GetDeviceByName(string deviceName)
    {
        var config = LoadDeviceList();
        return config.SaveDeviceList.FirstOrDefault(d => d.Name == deviceName);
    }

    /// <summary>根据端口获取设备配置</summary>
    public DeviceConfig? GetDeviceByPort(string port)
    {
        var config = LoadDeviceList();
        return config.SaveDeviceList.FirstOrDefault(d => d.Port == port);
    }

    /// <summary>获取所有设备配置列表</summary>
    public List<DeviceConfig> GetAllDevices()
    {
        var config = LoadDeviceList();
        return config.SaveDeviceList.ToList();
    }

    /// <summary>检查设备是否存在</summary>
    public bool DeviceExists(string deviceName)
    {
        var config = LoadDeviceList();
        return config.SaveDeviceList.Any(d => d.Name == deviceName);
    }

    /// <summary>清空所有设备配置</summary>
    public void ClearAllDevices()
    {
        var config = new SaveDeviceListConfig();
        SaveDeviceList(config);
    }

    /// <summary>获取设备总数</summary>
    public int GetDeviceCount()
    {
        var config = LoadDeviceList();
        return config.SaveDeviceList.Count;
    }
    #endregion
}

public interface IConfigJSONIO
{
    string ConfigItemName { get; set; }
    string ConfigItemValue { get; set; }
}

public class ConfigItem : IConfigJSONIO
{
    public string ConfigItemName { get; set; } = string.Empty;
    public string ConfigItemValue { get; set; } = string.Empty;

    public ConfigItem() { }

    public ConfigItem(string name, string value)
    {
        ConfigItemName = name;
        ConfigItemValue = value;
    }
}

public class StorageInfo
{
    public long TotalSize { get; set; }
    public long UsedSize { get; set; }
}

public class DeviceConfig
{
    public string Name { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string EncryptionMode { get; set; } = "NON";
    public StorageInfo Storage { get; set; } = new StorageInfo();
}

public class SaveDeviceListConfig
{
    public List<DeviceConfig> SaveDeviceList { get; set; } = new List<DeviceConfig>();
}