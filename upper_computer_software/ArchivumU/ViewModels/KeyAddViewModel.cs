using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArchivumU.ViewModels;

/// <summary>
/// 添加键值对窗口（KeyAddWindow）的视图模型。
/// 承载「键 / 值」输入及其校验状态。
/// </summary>
public class KeyAddViewModel : ViewModelBase, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    protected new void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public I18nViewModel I18n => I18nViewModel.Instance;

    private string _title = "Add Key";

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    private string _key = string.Empty;

    public string Key
    {
        get => _key;
        set
        {
            _key = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    private string _value = string.Empty;

    public string Value
    {
        get => _value;
        set
        {
            _value = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    /// <summary>键最大长度。</summary>
    public int KeyMaxLength { get; set; } = 32;

    /// <summary>值最大长度。</summary>
    public int ValueMaxLength { get; set; } = 64;

    /// <summary>是否可确认（键非空）。</summary>
    public bool CanConfirm => !string.IsNullOrWhiteSpace(Key);

    public KeyAddViewModel()
    {
        Title = I18n.KeyCreate;
    }

    public KeyAddViewModel(string key, string value) : this()
    {
        Key = key;
        Value = value;
    }
}
