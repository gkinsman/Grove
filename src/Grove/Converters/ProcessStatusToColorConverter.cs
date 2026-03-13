using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Grove.Core.Models;

namespace Grove.Converters;

public class ProcessStatusToColorConverter : IValueConverter
{
    public static readonly ProcessStatusToColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is ProcessStatus status ? status switch
        {
            ProcessStatus.Running => new SolidColorBrush(Color.Parse("#4EC9B0")),
            ProcessStatus.Error => new SolidColorBrush(Color.Parse("#F44747")),
            ProcessStatus.Starting => new SolidColorBrush(Color.Parse("#DCDCAA")),
            _ => new SolidColorBrush(Color.Parse("#808080")),
        } : new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
