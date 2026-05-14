using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvaloniaChatClient.Converters;

/// <summary>Returns "▼" when true (expanded) and "▶" when false (collapsed).</summary>
public class BoolToChevronConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "▼" : "▶";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
