using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ArchivumU.ViewModels;

namespace ArchivumU.Services;

public class ConfigJsonService
{
    // 配置文件路径：%AppData%/ArchivumU/configs.json
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions;
    
    // 全局静态单例模式
    public static ConfigJsonService Instance { get; } = new ConfigJsonService();

    public ConfigJsonService()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "ArchivumU");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "configs.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            AllowTrailingCommas = true
        };
    }

    #region 底层读写
    /// <summary>读取全部配置集合</summary>
    public List<ConfigItem> LoadAll()
    {
        if (!File.Exists(_filePath))
        if (!File.Exists(_filePath))
            return new List<ConfigItem>();

        string json = File.ReadAllText(_filePath);
        return JsonSerializer.Deserialize<List<ConfigItem>>(json, _jsonOptions) ?? new List<ConfigItem>();
    }

    /// <summary>覆盖写入全部配置集合</summary>
    private void SaveAll(List<ConfigItem> list)
    {
        string json = JsonSerializer.Serialize(list, _jsonOptions);
        File.WriteAllText(_filePath, json);
    }
    #endregion

    #region CURD 操作
    /// <summary>创建/新增配置（不存在则添加）</summary>
    public bool Create(ConfigItem item)
    {
        var list = LoadAll();
        bool exists = list.Any(x => x.ConfigItemName == item.ConfigItemName);
        if (exists) return false;

        list.Add(item);
        SaveAll(list);
        return true;
    }

    /// <summary>根据键查询单条配置，不存在返回null</summary>
    public ConfigItem? Read(string itemName)
    {
        var list = LoadAll();
        return list.FirstOrDefault(x => x.ConfigItemName == itemName);
    }

    /// <summary>更新指定键的值，不存在返回false</summary>
    public bool Update(string itemName, string newValue)
    {
        var list = LoadAll();
        var target = list.FirstOrDefault(x => x.ConfigItemName == itemName);
        if (target == null) return false;

        target.ConfigItemValue = newValue;
        SaveAll(list);
        return true;
    }

    /// <summary>删除指定键配置</summary>
    public bool Delete(string itemName)
    {
        var list = LoadAll();
        var target = list.FirstOrDefault(x => x.ConfigItemName == itemName);
        if (target == null) return false;

        list.Remove(target);
        SaveAll(list);
        return true;
    }

    /// <summary>根据键 存在则更新，不存在则新增（Upsert）</summary>
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
}

/// <summary>单条配置项基础接口</summary>
public interface IConfigJSONIO
{
    /// <summary>配置键名，唯一标识</summary>
    string ConfigItemName { get; set; }
    /// <summary>配置存储值</summary>
    string ConfigItemValue { get; set; }
}

/// <summary>单条配置实体，纯数据</summary>
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