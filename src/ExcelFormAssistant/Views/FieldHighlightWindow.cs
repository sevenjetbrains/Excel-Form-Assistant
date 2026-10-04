using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ExcelFormAssistant.ViewModels;

namespace ExcelFormAssistant.Views;

/// <summary>
/// Encadre à l'écran chaque champ trouvé, avec son numéro du rapport : vert s'il serait
/// rempli, orange si son libellé ne correspond à aucune colonne, rouge sans libellé, gris
/// s'il est ignoré. Les clics traversent cette fenêtre, et elle ne prend jamais le focus.
/// </summary>
public sealed class FieldHighlightWindow : Window
{
    private static readonly Brush Filled = Frozen(Color.FromRgb(0x1E, 0x7A, 0x3C));
    private static readonly Brush NoColumn = Frozen(Color.FromRgb(0xC9, 0x6A, 0x12));
    private static readonly Brush NoLabel = Frozen(Color.FromRgb(0xB3, 0x26, 0x1E));
    private static readonly Brush Ignored = Frozen(Color.FromRgb(0x7A, 0x80, 0x7C));

    private readonly Canvas _canvas = new();
    private readonly IReadOnlyList<FieldDiagnosticRow> _rows;

    public FieldHighlightWindow(IReadOnlyList<FieldDiagnosticRow> rows)
    {
        _rows = rows;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        IsHitTestVisible = false;
        Content = _canvas;
        FloatingWindowHelper.MakeNonActivating(this, clickThrough: true);
    }

    public void ShowOverScreens()
    {
        Show();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        UpdateLayout();
        Draw();
    }

    private void Draw()
    {
        _canvas.Children.Clear();
        foreach (var row in _rows)
        {
            // Les positions d'UI Automation sont en pixels écran : ramenées dans le repère WPF.
            var topLeft = _canvas.PointFromScreen(row.Field.Bounds.TopLeft);
            var bottomRight = _canvas.PointFromScreen(row.Field.Bounds.BottomRight);
            var area = new Rect(topLeft, bottomRight);
            var brush = BrushFor(row.Outcome);

            var frame = new Rectangle { Width = area.Width + 4, Height = area.Height + 4, Stroke = brush, StrokeThickness = 2 };
            Canvas.SetLeft(frame, area.X - 2);
            Canvas.SetTop(frame, area.Y - 2);
            _canvas.Children.Add(frame);

            var badge = new Border
            {
                Background = brush,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 0, 5, 1),
                Child = new TextBlock { Text = row.Number.ToString(), Foreground = Brushes.White, FontWeight = FontWeights.Bold, FontSize = 12 },
            };
            Canvas.SetLeft(badge, area.X - 2);
            Canvas.SetTop(badge, area.Y - 20);
            _canvas.Children.Add(badge);
        }
    }

    private static Brush BrushFor(FieldOutcome outcome) => outcome switch
    {
        FieldOutcome.WouldFill => Filled,
        FieldOutcome.NoColumn => NoColumn,
        FieldOutcome.NoLabel => NoLabel,
        _ => Ignored,
    };

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
