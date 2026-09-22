using System.Globalization;
using System.Windows.Data;
using ExcelFormAssistant.Models;

namespace ExcelFormAssistant.Views;

/// <summary>Affiche « — » à la place d'une cellule vide.</summary>
public sealed class EmptyToDashConverter : IValueConverter
{
    public static readonly EmptyToDashConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string { Length: > 0 } text ? text : ExcelRow.EmptyDisplay;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
