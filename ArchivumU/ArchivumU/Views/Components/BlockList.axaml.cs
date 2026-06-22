using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using ArchivumU.ViewModels;

namespace ArchivumU.Views.Components;

public partial class BlockList : UserControl
{
    private feature_string feature_string = new feature_string();
    public ObservableCollection<BlockItem> BlockItems { get; set; } = new ObservableCollection<BlockItem>();
    public BlockList()
    {
        InitializeComponent();
        BlockItems.Add(new BlockItem("password-bf","0x00",1,100,1024));
        BlockItems.Add(new BlockItem("password-google","0xEF",2,100,1024));
        BlockItems.Add(new BlockItem("password-yahoo","0xEF",3,100,1024));
        BlockItems.Add(new BlockItem("password-microsoft","0xEF",4,100,1024));
        BlockItems.Add(new BlockItem("password-pornhub","0xEF",5,100,1024));
        BlockItems.Add(new BlockItem("password-xvideo","0xEF",6,100,1024));
        
        LBBlockList.ItemsSource = BlockItems;
    }
    
    

    
}

public class BlockItem
{
    public string Name { get; set; }
    public string Address { get; set; } // 块地址
    public int KeyNumber { get; set; } // 键值对数量
    public long Size { get; set; }
    
    public string SizeString => feature_string.formatSizeToString(Size);


    public BlockItem()
    {
        
    }
    
    public BlockItem(string name, string address, int i, int keyNumber, long size)
    {
        Name = name;
        Address = address;
        KeyNumber = keyNumber;
        Size = size;
    }
}


