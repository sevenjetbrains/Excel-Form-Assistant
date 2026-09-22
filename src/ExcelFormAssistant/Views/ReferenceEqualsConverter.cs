using System.Globalization;
using System.Windows.Data;

namespace ExcelFormAssistant.Views;

/// <summary>true si les deux valeurs liées sont le même objet (ex. : cette ligne est la ligne active).</summary>
public sealed class ReferenceEqualsConverter : IMultiValueConverter
{
    public static readonly ReferenceEqualsConverter Instance = new();

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is not null && ReferenceEquals(values[0], values[1]);

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
