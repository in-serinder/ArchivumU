using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArchivumU.ViewModels;

/// <summary>
/// 添加块窗口（BlockAddWindow）的视图模型。
/// 仅承载「块名 / 块大小」输入及其校验状态，串口交互由窗口代码后台负责。
/// </summary>
public class BlockAddViewModel : ViewModelBase, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // i18n 实例
    public I18nViewModel I18n => I18nViewModel.Instance;

    private string _blockName = string.Empty;

    /// <summary>块名（设备端限制最大长度见 <see cref="NameMaxLength"/>）。</summary>
    public string BlockName
    {
        get => _blockName;
        set
        {
            // 依据设备约束裁剪长度
            if (value != null && value.Length > NameMaxLength)
            {
                value = value.Substring(0, NameMaxLength);
            }
            _blockName = value ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    private int _blockSize = 16;

    /// <summary>块大小（字节）。</summary>
    public int BlockSize
    {
        get => _blockSize;
        set
        {
            _blockSize = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    private string _title = "Add Block";

    /// <summary>窗口标题。</summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            OnPropertyChanged();
        }
    }

    /// <summary>块名最大长度（与设备端块名大小限制保持一致）。</summary>
    public int NameMaxLength { get; set; } = 17;

    /// <summary>块大小上限（字节）。</summary>
    public int SizeMax { get; set; } = 4096;

    /// <summary>是否可确认（块名非空且块大小合法）。</summary>
    public bool CanConfirm => !string.IsNullOrWhiteSpace(BlockName)
                              && BlockSize > 0
                              && BlockSize <= SizeMax;

    public BlockAddViewModel() { }

    public BlockAddViewModel(int nameMaxLength, int sizeMax)
    {
        NameMaxLength = nameMaxLength > 0 ? nameMaxLength : NameMaxLength;
        SizeMax = sizeMax > 0 ? sizeMax : SizeMax;
        Title = I18n.BlockCreate;
    }
}
