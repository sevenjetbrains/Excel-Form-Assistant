using System.Globalization;
using System.Windows.Data;

namespace ExcelFormAssistant.Views;

/// <summary>true si la valeur n'est pas null (ex. : activer un contrôle quand un fichier est ouvert).</summary>
public sealed class NotNullConverter : IValueConverter
{
    public static readonly NotNullConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
