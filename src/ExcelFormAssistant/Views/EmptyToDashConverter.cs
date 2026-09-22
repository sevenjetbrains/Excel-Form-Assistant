using System.Globalization;
using System.Windows.Data;

namespace ExcelFormAssistant.Views;

/// <summary>Affiche « — » à la place d'une cellule vide.</summary>
public sealed class EmptyToDashConverter : IValueConverter
{
    public const string Dash = "—";

    public static readonly EmptyToDashConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } text ? text : Dash;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
