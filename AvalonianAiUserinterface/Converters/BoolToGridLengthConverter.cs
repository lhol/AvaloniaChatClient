using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Avaiu.Converters;

/// <summary>Converts a bool to a GridLength. True = collapsed width, False = expanded width.</summary>
public class BoolToGridLengthConverter : IValueConverter
{
    public GridLength CollapsedLength { get; set; } = new GridLength(52);
    public GridLength ExpandedLength { get; set; } = new GridLength(220);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? CollapsedLength : ExpandedLength;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => AvaloniaProperty.UnsetValue;
}
