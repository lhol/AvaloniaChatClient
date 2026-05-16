using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Avaiu.Converters;

/// <summary>Returns true when the bound enum value equals the ConverterParameter string.</summary>
public class EnumEqualityConverter : IValueConverter
{
    public static readonly EnumEqualityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return false;
        return value.ToString() == parameter.ToString();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
