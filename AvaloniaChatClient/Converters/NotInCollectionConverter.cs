using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace AvaloniaChatClient.Converters;

/// <summary>
/// F-01: Returns true when a session GUID is NOT present in the OpenSessionIds collection.
/// Usage: IsEnabled="{Binding Id, Converter={x:Static conv:NotInCollectionConverter.Instance},
///                   ConverterParameter={Binding OpenSessionIds}}"
/// Because Avalonia does not support ConverterParameter bindings directly, we use
/// a MultiBinding via NotInCollectionMultiConverter.
/// </summary>
public sealed class NotInCollectionConverter : IMultiValueConverter
{
    public static readonly NotInCollectionConverter Instance = new();

    /// values[0] = Guid (session Id from DataTemplate)
    /// values[1] = IEnumerable<Guid> (OpenSessionIds from MainViewModel)
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return true;
        if (values[0] is not Guid id) return true;
        if (values[1] is not System.Collections.IEnumerable col) return true;

        foreach (var item in col)
            if (item is Guid g && g == id)
                return false;

        return true;
    }
}
