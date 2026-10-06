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
/// <item>sélecteur de date : l'écriture est acceptée sans rien changer — il faut taper. Chrome
/// l'expose en champ de saisie, Firefox en groupe sans valeur : dans les deux cas ce sont ses
/// trois cases jour / mois / année qui le font reconnaître ;</item>
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

    /// <summary>Niveaux remontés pour retrouver le champ : texte → case → groupe, chez Firefox.</summary>
    private const int MaxPromoteLevels = 3;

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
                FieldKind.Date => new TargetField(kind, FocusPoint: DayBox(field)),
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

    /// <summary>
    /// Remonte de la case jour / mois / année vers le sélecteur de date qui la porte. La
    /// profondeur dépend du navigateur : Chrome place les trois cases directement dans le
    /// champ, Firefox glisse en plus un texte (« jj », « mm », « aaaa ») dans chacune et
    /// enveloppe le tout dans un groupe. On remonte donc jusqu'à trouver l'ancêtre qui porte
    /// les trois cases, sans dépasser le formulaire alentour.
    /// </summary>
    private static AutomationElement Promote(AutomationElement element)
    {
        var type = element.Current.ControlType;
        if (type != ControlType.Spinner && type != ControlType.Text)
            return element;

        var candidate = element;
        for (int level = 0; level < MaxPromoteLevels; level++)
        {
            if (TreeWalker.ControlViewWalker.GetParent(candidate) is not AutomationElement parent)
                break;

            candidate = parent;
            if (CountSpinners(candidate) >= 3)
                return candidate;
        }

        return element;
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

    /// <summary>
    /// Où cliquer pour que la frappe commence au jour : la case du jour elle-même, dont
    /// l'accessibilité donne la position. Viser le bord gauche du champ ne marcherait pas sur
    /// un formulaire écrit de droite à gauche, où le bouton du calendrier se trouve à gauche —
    /// le clic l'ouvrirait au lieu de saisir.
    /// </summary>
    private static Point? DayBox(AutomationElement element)
    {
        var boxes = element.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Spinner));

        // Les cases sont dans l'ordre du document : jour, puis mois, puis année.
        if (boxes.Count > 0 && Center(boxes[0].Current.BoundingRectangle) is Point box)
            return box;

        return LeftEdge(element);
    }

    private static Point? Center(Rect rect) =>
        rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0 || double.IsInfinity(rect.Left) || double.IsInfinity(rect.Top)
            ? null
            : new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

    /// <summary>Repli quand les cases n'ont pas de position connue.</summary>
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
