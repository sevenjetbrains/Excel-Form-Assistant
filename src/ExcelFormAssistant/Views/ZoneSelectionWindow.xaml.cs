using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ExcelFormAssistant.Views;

/// <summary>
/// Voile sur tous les écrans, sur lequel on trace à la souris le rectangle qui entoure
/// les champs du formulaire. Échap ou un clic droit annule.
/// </summary>
public partial class ZoneSelectionWindow : Window
{
    /// <summary>En dessous, c'est un clic, pas une zone.</summary>
    private const double MinSize = 12;

    private Point? _start;

    private ZoneSelectionWindow()
    {
        InitializeComponent();

        // Couvre l'ensemble des écrans branchés.
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        Loaded += (_, _) =>
        {
            // Consigne en haut de l'écran principal.
            Hint.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var primaryCenter = PointFromScreenDip(SystemParameters.PrimaryScreenWidth / 2, 0);
            Canvas.SetLeft(Hint, primaryCenter.X - Hint.DesiredSize.Width / 2);
            Canvas.SetTop(Hint, primaryCenter.Y + 24);
            Activate();
            Focus();
        };
    }

    /// <summary>Zone tracée, en pixels écran ; null si l'utilisateur a annulé.</summary>
    public Rect? SelectedZone { get; private set; }

    /// <summary>Affiche le voile et attend le tracé.</summary>
    public static Rect? Select()
    {
        var window = new ZoneSelectionWindow();
        window.ShowDialog();
        return window.SelectedZone;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
        base.OnKeyDown(e);
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) => Close();

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _start = e.GetPosition(Surface);
        Selection.Visibility = Visibility.Visible;
        Update(_start.Value);
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_start is not null)
            Update(e.GetPosition(Surface));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_start is not Point start)
            return;
        ReleaseMouseCapture();
        var end = e.GetPosition(Surface);
        _start = null;

        var area = new Rect(start, end);
        if (area.Width < MinSize || area.Height < MinSize)
        {
            Selection.Visibility = Visibility.Collapsed; // simple clic : on recommence
            return;
        }

        // Conversion en pixels écran, l'unité d'UI Automation.
        var topLeft = Surface.PointToScreen(area.TopLeft);
        var bottomRight = Surface.PointToScreen(area.BottomRight);
        SelectedZone = new Rect(topLeft, bottomRight);
        Close();
    }

    private void Update(Point current)
    {
        var area = new Rect(_start!.Value, current);
        Canvas.SetLeft(Selection, area.X);
        Canvas.SetTop(Selection, area.Y);
        Selection.Width = area.Width;
        Selection.Height = area.Height;
    }

    /// <summary>Point de l'écran exprimé en unités WPF, ramené dans le repère du voile.</summary>
    private Point PointFromScreenDip(double x, double y) =>
        new(x - SystemParameters.VirtualScreenLeft, y - SystemParameters.VirtualScreenTop);
}
