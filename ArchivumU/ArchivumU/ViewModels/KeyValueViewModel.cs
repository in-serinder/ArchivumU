using System.Collections.ObjectModel;

namespace ArchivumU.ViewModels;

public class KeyValueViewModel
{
    
    
    // i18n 实例
    public I18nViewModel I18n => I18nViewModel.Instance;
    
    public ObservableCollection<KeyValueItem> KeyValueItems { get; set; } = new ObservableCollection<KeyValueItem>();

    public KeyValueViewModel()
    {
        
        // 添加示例数据
        KeyValueItems.Add(new KeyValueItem("username","admin",10,"0x10000000"));
        KeyValueItems.Add(new KeyValueItem("password","123456",10,"0x20000000"));
        KeyValueItems.Add(new KeyValueItem("email","admin@example.com",10,"0x30000000"));
    }
}



public class KeyValueItem
{
    public string Key { get; set; }
    public string Value { get; set; }
    public int Size { get; set; }
    public int Length => Value?.Length ?? 0;
    public string Address { get; set; }
    public string SizeString => feature_string.formatSizeToString(Value?.Length ?? 0);
    // i18n 实例
    public I18nViewModel I18n => I18nViewModel.Instance;

    public KeyValueItem() { }
    
    public KeyValueItem(string key, string value, int size, string address)
    {
        Key = key;
        Value = value;
        Size = size;
        Address = address;
    }
}