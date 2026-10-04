using System.Windows;
using System.Windows.Automation;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Reconnaît le champ sous le curseur à travers l'API d'accessibilité, qui expose le contenu
/// des pages web : un champ de saisie ordinaire peut recevoir sa valeur directement, un
/// sélecteur de date doit être tapé.
///
/// Toute panne de ce côté (application sans accessibilité, page lente, élément disparu) rend
/// un champ « inconnu » : le remplissage retombe alors sur le presse-papiers et Ctrl+V.
/// </summary>
public sealed class FieldInspector
{
    /// <summary>Marge depuis le bord gauche d'une date, pour tomber sur la case du jour.</summary>
    private const int DayOffset = 12;

    public TargetField Inspect(Point cursor)
    {
        try
        {
            var element = AutomationElement.FromPoint(cursor);
            if (element is null)
                return TargetField.Unknown;

            // Le curseur peut tomber sur une case jour / mois / année : le champ est son parent.
            if (element.Current.ControlType == ControlType.Spinner
                && TreeWalker.ControlViewWalker.GetParent(element) is AutomationElement parent)
                element = parent;

            var value = GetValuePattern(element);
            int spinners = CountSpinners(element);
            var kind = TargetField.KindOf(value is not null, value?.Current.IsReadOnly ?? true, spinners);

            return kind switch
            {
                FieldKind.Date => new TargetField(kind, FocusPoint: LeftEdge(element)),
                FieldKind.Text => new TargetField(kind, Write: text => TryWrite(value!, element, text)),
                _ => TargetField.Unknown,
            };
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException
                                      or ArgumentException or TimeoutException)
        {
            return TargetField.Unknown;
        }
    }

    private static ValuePattern? GetValuePattern(AutomationElement element) =>
        element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) ? (ValuePattern)pattern : null;

    private static int CountSpinners(AutomationElement element) =>
        element.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Spinner)).Count;

    /// <summary>
    /// Écrit puis relit : l'accessibilité accepte parfois l'écriture sans rien changer
    /// (c'est le cas des sélecteurs de date), et il ne faut pas annoncer un succès à tort.
    /// </summary>
    private static bool TryWrite(ValuePattern value, AutomationElement element, string text)
    {
        try
        {
            value.SetValue(text);
            return GetValuePattern(element)?.Current.Value == text;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException)
        {
            return false;
        }
    }

    private static Point? LeftEdge(AutomationElement element)
    {
        var rect = element.Current.BoundingRectangle;
        if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
            return null;

        return new Point(rect.Left + Math.Min(DayOffset, rect.Width / 3), rect.Top + rect.Height / 2);
    }
}
