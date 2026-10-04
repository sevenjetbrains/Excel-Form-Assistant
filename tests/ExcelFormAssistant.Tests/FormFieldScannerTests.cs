using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using ExcelFormAssistant.Models;
using ExcelFormAssistant.Services;

namespace ExcelFormAssistant.Tests;

/// <summary>
/// Le scanner face à une vraie fenêtre : un petit formulaire WPF affiché quelques instants,
/// lu par UI Automation comme le serait un formulaire d'une autre application.
/// Aucun clic ni aucune touche n'est envoyé.
/// </summary>
public sealed class FormFieldScannerTests
{
    [Fact]
    public void Scan_FindsFieldsAndTheirLabels() => RunOnSta(() =>
    {
        var named = new TextBox { Width = 180 };
        AutomationProperties.SetName(named, "Nom");

        var nearby = new TextBox { Width = 180 };       // aucun nom : libellé à trouver à gauche
        var password = new PasswordBox { Width = 180 };
        AutomationProperties.SetName(password, "Mot de passe");
        var filled = new TextBox { Width = 180, Text = "déjà saisi" };
        AutomationProperties.SetName(filled, "Prénom");

        var grid = new Grid { Margin = new Thickness(20) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        void AddRow(UIElement label, UIElement field)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            int row = grid.RowDefinitions.Count - 1;
            Grid.SetRow(label, row);
            Grid.SetRow(field, row);
            Grid.SetColumn(field, 1);
            grid.Children.Add(label);
            grid.Children.Add(field);
        }
        AddRow(new TextBlock { Text = "Nom" }, named);
        AddRow(new TextBlock { Text = "Téléphone :", VerticalAlignment = VerticalAlignment.Center }, nearby);
        AddRow(new TextBlock { Text = "Mot de passe" }, password);
        AddRow(new TextBlock { Text = "Prénom" }, filled);

        var window = new Window
        {
            Title = "Formulaire de test",
            Content = grid,
            Width = 360,
            Height = 230,
            Topmost = true,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
        };
        window.Show();
        DoEvents();

        var topLeft = window.PointToScreen(new Point(0, 0));
        var bottomRight = window.PointToScreen(new Point(window.ActualWidth, window.ActualHeight));
        var zone = new Rect(topLeft, bottomRight);

        // UI Automation interroge cette fenêtre : le thread d'interface doit continuer à tourner.
        var scan = Task.Run(() => new FormFieldScanner().Scan(zone));
        while (!scan.IsCompleted)
            DoEvents();
        window.Close();

        var fields = scan.Result;
        Assert.Equal(4, fields.Count);

        Assert.Equal(("Nom", LabelSource.AccessibleName), (fields[0].Label, fields[0].LabelSource));
        Assert.Equal(("Téléphone :", LabelSource.NearbyText), (fields[1].Label, fields[1].LabelSource));
        Assert.True(fields[2].IsPassword);
        Assert.False(fields[2].HasValue); // la valeur d'un mot de passe n'est jamais lue
        Assert.Equal("Prénom", fields[3].Label);
        Assert.True(fields[3].HasValue);
    });

    private static void DoEvents()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void RunOnSta(Action test)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { test(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
