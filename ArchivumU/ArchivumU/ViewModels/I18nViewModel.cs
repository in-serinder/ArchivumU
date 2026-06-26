using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Resources;
using ArchivumU.I18n;
using ArchivumU.Services;
using CommunityToolkit.Mvvm.Input;

namespace ArchivumU.ViewModels;

public class I18nViewModel : ViewModelBase
{
    
    
    //全局静态单例模式
    public static I18nViewModel Instance { get; } = new I18nViewModel();
    
    public ConfigJsonService ConfigService => ConfigJsonService.Instance;
    
    private readonly Assembly _assembly;
    private readonly string _baseName;

    public I18nViewModel()
    {
        _assembly = typeof(Strings).Assembly;
        _baseName = "ArchivumU.I18n.Strings";
        Strings.Culture = CultureInfo.CurrentUICulture;

        //预先
        // Strings.Culture = new CultureInfo("en-US");
        //存在性检查
        if (ConfigService.Read("Language") == null)
        {
            ConfigService.Create(new ConfigItem("Language", "en-US"));

        }
        //读取配置   
        Strings.Culture = new CultureInfo(ConfigService.Read("Language")?.ConfigItemValue);
        
     
    }
    
    

        public void SetEnglish()
        {
            Debug.WriteLine("\n=== Setting English ===");
            Strings.Culture = new CultureInfo("en-US");
            ConfigService.Update("Language", "en-US");
            RefreshAllProperties(); // 刷新所有属性
        }


        public void SetChinese()
        {
            Debug.WriteLine("\n=== Setting Chinese ===");
            Strings.Culture = new CultureInfo("zh-CN");
            ConfigService.Update("Language", "zh-CN");
            RefreshAllProperties(); // 刷新所有属性
        }

        // 手动刷新所有本地化属性
        private void RefreshAllProperties()
        {
 // 基础菜单
            OnPropertyChanged(nameof(File));
            OnPropertyChanged(nameof(Save));
            OnPropertyChanged(nameof(SaveAs));
            OnPropertyChanged(nameof(Exit));
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(English));
            OnPropertyChanged(nameof(Chinese));
            OnPropertyChanged(nameof(More));
            OnPropertyChanged(nameof(Help));
            OnPropertyChanged(nameof(About));
            
            // 设备相关
            OnPropertyChanged(nameof(Device));
            OnPropertyChanged(nameof(DeviceFormat));
            OnPropertyChanged(nameof(DeviceBackups));
            OnPropertyChanged(nameof(DeviceRemove));
            OnPropertyChanged(nameof(DeviceBackupsImport));
            OnPropertyChanged(nameof(DeviceClone));
            OnPropertyChanged(nameof(CloneOptions));
            OnPropertyChanged(nameof(CloneStart));
            OnPropertyChanged(nameof(SaveSourceFile));
            OnPropertyChanged(nameof(NotClone));
            OnPropertyChanged(nameof(TargetDevice));
            OnPropertyChanged(nameof(SourceDevice));
            OnPropertyChanged(nameof(TargetDeviceInfo));
            OnPropertyChanged(nameof(SourceDeviceInfo));
            
            // 存储相关
            OnPropertyChanged(nameof(TotalStorage));
            OnPropertyChanged(nameof(UsedStorage));
            OnPropertyChanged(nameof(AvailableStorage));
            OnPropertyChanged(nameof(Prop));
            
            // 块相关
            OnPropertyChanged(nameof(Block));
            OnPropertyChanged(nameof(BlockLformat));
            OnPropertyChanged(nameof(BlockCreate));
            OnPropertyChanged(nameof(BlockDel));
            OnPropertyChanged(nameof(BlockRefresh));
            OnPropertyChanged(nameof(BlockRename));
            OnPropertyChanged(nameof(SelectBlockDel));
            OnPropertyChanged(nameof(BlockDiscover));
            
            // 键相关
            OnPropertyChanged(nameof(Key));
            OnPropertyChanged(nameof(KeyCreate));
            OnPropertyChanged(nameof(KeyRemove));
            OnPropertyChanged(nameof(KeyValue));
            
            // 通用
            OnPropertyChanged(nameof(Refresh));
            OnPropertyChanged(nameof(Add));
            OnPropertyChanged(nameof(Remove));
            OnPropertyChanged(nameof(Rename));
            OnPropertyChanged(nameof(Config));
            OnPropertyChanged(nameof(AutoConnect));
            OnPropertyChanged(nameof(AutoBackups));
            OnPropertyChanged(nameof(BackupToLocal));
            OnPropertyChanged(nameof(Confirm));
            OnPropertyChanged(nameof(Del));
            OnPropertyChanged(nameof(Password));
            OnPropertyChanged(nameof(Encryption));
            OnPropertyChanged(nameof(Decryption));
            OnPropertyChanged(nameof(Cancel));
            OnPropertyChanged(nameof(Cell));
            OnPropertyChanged(nameof(Unknown));
            OnPropertyChanged(nameof(Typei8));
            OnPropertyChanged(nameof(Zone));
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(NoneContent));
            OnPropertyChanged(nameof(Init));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(DevName));
            OnPropertyChanged(nameof(Umount));
            OnPropertyChanged(nameof(Mount));
            OnPropertyChanged(nameof(Reset));
            
            // 状态和错误
            OnPropertyChanged(nameof(Size));
            OnPropertyChanged(nameof(Len));
            OnPropertyChanged(nameof(Address));
            OnPropertyChanged(nameof(Connected));
            OnPropertyChanged(nameof(Disconnected));
            OnPropertyChanged(nameof(Warn));
            OnPropertyChanged(nameof(Err));
            OnPropertyChanged(nameof(ConfirmFormatting));
            OnPropertyChanged(nameof(OperationCannotBeRecovered));
            OnPropertyChanged(nameof(OpenBackupFile));
            OnPropertyChanged(nameof(SaveFileLocation));
            OnPropertyChanged(nameof(StorageFragmentDefragmentation));
            OnPropertyChanged(nameof(FragmentDefragmentation));
            OnPropertyChanged(nameof(StorageAbnormal));
            OnPropertyChanged(nameof(InsufficientSpace));
            OnPropertyChanged(nameof(AddressError));
            OnPropertyChanged(nameof(ChipNoResponse));
            OnPropertyChanged(nameof(SerialPortDisconnected));
            OnPropertyChanged(nameof(Authentication));
            OnPropertyChanged(nameof(AuthenticationFailed));
            
        }

        private string GetString(string key)
        {
            CultureInfo culture = Strings.Culture;
            
            // 1. 尝试查找特定文化的资源
            string cultureResourceName = $"{_baseName}-{culture.Name.Replace("-", "_")}.resources";
            string value = TryGetStringFromResource(cultureResourceName, key);
            
            if (value != null)
                return value;
            
            // 2. 如果是特定文化，尝试父文化
            if (!string.IsNullOrEmpty(culture.Parent?.Name))
            {
                string parentResourceName = $"{_baseName}-{culture.Parent.Name.Replace("-", "_")}.resources";
                value = TryGetStringFromResource(parentResourceName, key);
                if (value != null)
                    return value;
            }
            
            // 3. 回退到默认资源
            return TryGetStringFromResource($"{_baseName}.resources", key) ?? key;
        }

        private string TryGetStringFromResource(string resourceName, string key)
        {
            try
            {
                using (var stream = _assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Debug.WriteLine($"Resource stream null: {resourceName}");
                        return null;
                    }
                    
                    using (var reader = new ResourceReader(stream))
                    {
                        foreach (System.Collections.DictionaryEntry entry in reader)
                        {
                            if (entry.Key.ToString() == key)
                            {
                                return entry.Value.ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error: {ex.Message}");
            }
            
            return null;
        }
        
        // 所有属性都调用 GetString
   
        // 基础菜单
        public string File => GetString("_File");
        public string Save => GetString("_Save");
        public string SaveAs => GetString("_SaveAs");
        public string Exit => GetString("_Exit");
        public string Language => GetString("_Language");
        public string English => GetString("_English");
        public string Chinese => GetString("_Chinese");
        public string More => GetString("_More");
        public string Help => GetString("_Help");
        public string About => GetString("_About");
        
        // 设备相关
        public string Device => GetString("_device");
        public string DeviceFormat => GetString("_device_format");
        public string DeviceBackups => GetString("_device_backups");
        public string DeviceRemove => GetString("_device_remove");
        public string DeviceBackupsImport => GetString("_device_backups_import");
        public string DeviceClone => GetString("_device_clone");
        public string CloneOptions => GetString("_clone_options");
        public string CloneStart => GetString("_clone_start");
        public string SaveSourceFile => GetString("_save_source_file");
        public string SkipExisting => GetString("_skip_existing");
        public string NotClone => GetString("_non_started");
        public string TargetDevice => GetString("_target_dev");
        public string SourceDevice => GetString("_source_dev");   
        public string TargetDeviceInfo => GetString("_target_dev_info");
        public string SourceDeviceInfo => GetString("_source_dev_info");   
        
        // 存储相关
        public string TotalStorage => GetString("_total_storage");
        public string UsedStorage => GetString("_used_storge");
        public string AvailableStorage => GetString("_availabe_storage");
        public string Prop => GetString("_prop");
        
        // 块相关
        public string Block => GetString("_block");
        public string BlockLformat => GetString("_block_lformat");
        public string BlockCreate => GetString("_block_create");
        public string BlockDel => GetString("_block_del");
        public string BlockRefresh => GetString("_block_refresh");
        public string BlockRename => GetString("_block_rename");
        public string SelectBlockDel => GetString("_select_block_del");
        public string BlockDiscover => GetString("_block_discover");
        
        // 键相关
        public string Key => GetString("_key");
        public string KeyCreate => GetString("_key_create");
        public string KeyRemove => GetString("_key_remove");
        public string KeyValue => GetString("_key_value");
        
        // 通用
        public string Refresh => GetString("_Refresh");
        public string Add => GetString("_Add");
        public string Remove => GetString("_Remove");
        public string Rename => GetString("_Rename");
        public string Config => GetString("_config");
        public string AutoConnect => GetString("_auto_connect");
        public string AutoBackups => GetString("_auto_backups");
        public string BackupToLocal => GetString("_BackupToLocal");
        public string Confirm => GetString("_confirm");
        public string Del => GetString("_del");
        public string Password => GetString("_password");
        public string Encryption => GetString("_encryption");
        public string Decryption => GetString("_decryption");
        public string Authentication => GetString("_authentication");
        public string AuthenticationFailed => GetString("_authentication_failed");
        public string Cancel => GetString("_cancel");
        public string Cell => GetString("_cell");
        public string Unknown => GetString("_unknow");
        public string Typei8 => GetString("_type");
        public string Zone => GetString("_zone");
        public string Value => GetString("_value");
        public string Count => GetString("_count");
        public string NoneContent => GetString("_non_content");
        public string Init => GetString("_init");
        public string InitDev => GetString("_init_dev");
        public string Name => GetString("_name");
        public string DevName => GetString("_dev_name");
        public string Umount => GetString("_umount");
        public string Mount => GetString("_mount");
        public string Reset => GetString("_reset");
        
        // 状态和错误
        public string Size => GetString("_size");
        public string Len => GetString("_len");
        public string Address => GetString("_address");
        public string Connected => GetString("_connected");
        public string Disconnected => GetString("_disconnected");
        public string Warn => GetString("_warn");
        public string Err => GetString("_err");
        public string ConfirmFormatting => GetString("_confirm_formatting");
        public string OperationCannotBeRecovered => GetString("_operation_cannot_be_recovered");
        public string OpenBackupFile => GetString("_open_backup_file");
        public string SaveFileLocation => GetString("_save_file_location");
        public string StorageFragmentDefragmentation => GetString("_storage_fragment_defragmentation");
        public string FragmentDefragmentation => GetString("_fragment_defragmentation");
        public string StorageAbnormal => GetString("_storage_abnormal");
        public string InsufficientSpace => GetString("_insufficient_space");
        public string AddressError => GetString("_address_error");
        public string ChipNoResponse => GetString("_chip_no_response");
        public string SerialPortDisconnected => GetString("_serial_port_disconnected");
        
    
}