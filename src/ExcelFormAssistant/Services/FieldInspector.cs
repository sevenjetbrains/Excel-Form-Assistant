using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Reconnaît le champ sous le curseur à travers l'API d'accessibilité, qui expose le contenu
/// des pages web, et sait le remplir quand c'est possible.
///
/// Ce que chaque sorte de champ accepte a été mesuré sur Chrome :
/// <list type="bullet">
/// <item>champ de saisie : la valeur s'y écrit, et la page reçoit son événement de saisie ;</item>
/// <item>sélecteur de date : l'écriture est acceptée sans rien changer — il faut taper ;</item>
/// <item>liste déroulante : laissée tranquille, voir <see cref="TargetField.KindOf"/>.</item>
/// </list>
///
/// Toute panne de ce côté (application sans accessibilité, page lente, élément disparu) rend
/// un champ « inconnu » : le remplissage retombe alors sur le presse-papiers et Ctrl+V.
/// </summary>
public sealed class FieldInspector
{
    /// <summary>Marge depuis le bord gauche d'une date, pour tomber sur la case du jour.</summary>
    private const int DayOffset = 12;

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// Délai laissé à la page pour montrer la valeur écrite. Sans cette attente, la relecture
    /// tombe avant la mise à jour, l'écriture passe pour un échec, et le Ctrl+V de repli met
    /// la valeur en double.
    /// </summary>
    private static readonly TimeSpan WriteTimeout = TimeSpan.FromMilliseconds(400);

    public TargetField Inspect(Point cursor)
    {
        try
        {
            var element = AutomationElement.FromPoint(cursor);
            if (element is null)
                return TargetField.Unknown;

            // Le curseur peut tomber sur une case jour / mois / année : le champ est son parent.
            element = Promote(element);

            var value = GetValuePattern(element);
            var kind = TargetField.KindOf(value is not null, value?.Current.IsReadOnly ?? true,
                CountSpinners(element), IsList(element));

            var field = element;
            return kind switch
            {
                FieldKind.Date => new TargetField(kind, FocusPoint: LeftEdge(field)),
                FieldKind.Text => new TargetField(kind, Fill: text => Write(value!, field, text)),
                _ => TargetField.Unknown,
            };
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return TargetField.Unknown;
        }
    }

    /// <summary>Une liste déroulante : un &lt;select&gt;, ou l'habillage d'un Select2.</summary>
    private static bool IsList(AutomationElement element) =>
        element.Current.ControlType == ControlType.ComboBox;

    /// <summary>Remonte de la case jour / mois / année vers le sélecteur de date qui la porte.</summary>
    private static AutomationElement Promote(AutomationElement element)
    {
        if (element.Current.ControlType != ControlType.Spinner)
            return element;

        return TreeWalker.ControlViewWalker.GetParent(element) is AutomationElement parent
            && CountSpinners(parent) >= 3
                ? parent
                : element;
    }

    private static ValuePattern? GetValuePattern(AutomationElement element) =>
        element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) ? (ValuePattern)pattern : null;

    private static int CountSpinners(AutomationElement element) =>
        element.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Spinner)).Count;

    /// <summary>
    /// Écrit puis relit : l'accessibilité accepte parfois l'écriture sans rien changer
    /// (c'est le cas des sélecteurs de date), et il ne faut pas annoncer un succès à tort.
    /// La relecture est réessayée : la page ne se met pas à jour dans l'instant.
    /// </summary>
    private static FillResult Write(ValuePattern value, AutomationElement element, string text)
    {
        try
        {
            string? before = ReadValue(element);
            value.SetValue(text);

            var deadline = DateTime.UtcNow + WriteTimeout;
            string? after;
            do
            {
                after = ReadValue(element);
                if (after == text)
                    return FillResult.Done;

                Thread.Sleep(PollInterval);
            }
            while (DateTime.UtcNow < deadline);

            return TargetField.Verdict(before, after, text);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return FillResult.Failed;
        }
    }

    private static string? ReadValue(AutomationElement element) => GetValuePattern(element)?.Current.Value;

    private static Point? LeftEdge(AutomationElement element)
    {
        var rect = element.Current.BoundingRectangle;
        if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
            return null;

        return new Point(rect.Left + Math.Min(DayOffset, rect.Width / 3), rect.Top + rect.Height / 2);
    }

    /// <summary>Pannes normales du côté de l'accessibilité : la page bouge, l'élément s'en va.</summary>
    private static bool IsExpected(Exception ex) =>
        ex is ElementNotAvailableException or ElementNotEnabledException or InvalidOperationException
            or ArgumentException or TimeoutException or COMException;
}
