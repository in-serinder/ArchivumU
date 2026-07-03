namespace ArchivumU.ViewModels;

public class feature_string
{
    public static string formatSizeToString(long size)
    {
        const long unit = 1024;
        if (size < unit)
        {
            return $"{size} B";
        }
        if (size < unit * unit)
        {
            return $"{(double)size / unit:F2} KB";
        }
        if (size < unit * unit * unit)
        {
            return $"{(double)size / (unit * unit):F2} MB";
        }
        // GB分支补上
        return $"{(double)size / (unit * unit * unit):F2} GB";
    }
}