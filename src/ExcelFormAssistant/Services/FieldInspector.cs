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
/// <item>liste déroulante (y compris Select2) : s'ouvre, se cherche, et l'option s'actionne.</item>
/// </list>
///
/// Toute panne de ce côté (application sans accessibilité, page lente, élément disparu) rend
/// un champ « inconnu » : le remplissage retombe alors sur le presse-papiers et Ctrl+V.
/// </summary>
public sealed class FieldInspector
{
    /// <summary>Marge depuis le bord gauche d'une date, pour tomber sur la case du jour.</summary>
    private const int DayOffset = 12;

    /// <summary>
    /// Attente des options d'une liste. Celles de Select2 n'arrivent qu'après la recherche, et
    /// souvent d'une requête au serveur : d'où ce délai, assez long pour une page lente et
    /// assez court pour ne pas figer l'application.
    /// </summary>
    private static readonly TimeSpan OptionsTimeout = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    public TargetField Inspect(Point cursor)
    {
        try
        {
            var element = AutomationElement.FromPoint(cursor);
            if (element is null)
                return TargetField.Unknown;

            // Le curseur peut tomber sur une case jour / mois / année, ou sur le libellé
            // intérieur d'une liste : le champ lui-même est alors un parent.
            element = Promote(element);

            var value = GetValuePattern(element);
            bool canExpand = Supports(element, ExpandCollapsePattern.Pattern);
            var kind = TargetField.KindOf(value is not null, value?.Current.IsReadOnly ?? true,
                CountSpinners(element), canExpand);

            var field = element;
            return kind switch
            {
                FieldKind.Date => new TargetField(kind, FocusPoint: LeftEdge(field)),
                FieldKind.List => new TargetField(kind, Fill: text => ChooseInList(field, text)),
                FieldKind.Text => new TargetField(kind, Fill: text => Write(value!, field, text)),
                _ => TargetField.Unknown,
            };
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return TargetField.Unknown;
        }
    }

    /// <summary>Remonte de la case ou du libellé intérieur vers le champ qui les porte.</summary>
    private static AutomationElement Promote(AutomationElement element)
    {
        var type = element.Current.ControlType;
        if (type != ControlType.Spinner && type != ControlType.Text && type != ControlType.Edit)
            return element;

        // Deux niveaux suffisent : Select2 place son libellé dans la ComboBox.
        var candidate = element;
        for (int level = 0; level < 2; level++)
        {
            if (TreeWalker.ControlViewWalker.GetParent(candidate) is not AutomationElement parent)
                break;

            candidate = parent;
            if (Supports(candidate, ExpandCollapsePattern.Pattern) || CountSpinners(candidate) >= 3)
                return candidate;
        }

        return element;
    }

    /// <summary>
    /// Ouvre la liste, y écrit la recherche si elle en a une, puis actionne l'option qui
    /// correspond. Si plusieurs restent possibles, la liste est laissée ouverte et filtrée
    /// plutôt que de choisir au hasard.
    /// </summary>
    private static FillResult ChooseInList(AutomationElement combo, string value)
    {
        try
        {
            if (combo.GetCurrentPattern(ExpandCollapsePattern.Pattern) is not ExpandCollapsePattern expand)
                return FillResult.Failed;

            expand.Expand();

            // Les listes qui se cherchent n'affichent leurs options qu'après la saisie.
            if (FindSearchField(combo) is AutomationElement search && GetValuePattern(search) is ValuePattern box)
                Write(box, search, value);

            var options = WaitForOptions(combo);
            if (options.Count == 0)
            {
                expand.Collapse();
                return FillResult.Failed;
            }

            var chosen = options.FirstOrDefault(o => TargetField.SameLabel(o.Current.Name, value))
                ?? (options.Count == 1 ? options[0] : null);
            if (chosen is null)
                return FillResult.Narrowed; // liste laissée ouverte : à l'utilisateur de trancher

            if (chosen.GetCurrentPattern(InvokePattern.Pattern) is not InvokePattern invoke)
                return FillResult.Narrowed;

            invoke.Invoke();
            return FillResult.Done;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return FillResult.Failed;
        }
    }

    /// <summary>Champ de recherche d'une liste, s'il en a un (Select2 et compagnie).</summary>
    private static AutomationElement? FindSearchField(AutomationElement combo) =>
        Search(combo, ControlType.Edit).FirstOrDefault(IsSearchBox)
        ?? Search(TopLevel(combo), ControlType.Edit).FirstOrDefault(IsSearchBox);

    private static bool IsSearchBox(AutomationElement element) =>
        element.Current.ClassName.Contains("search", StringComparison.OrdinalIgnoreCase);

    private static List<AutomationElement> WaitForOptions(AutomationElement combo)
    {
        var deadline = DateTime.UtcNow + OptionsTimeout;
        while (true)
        {
            // Les options d'une liste web sont souvent posées à côté du champ, pas dedans :
            // on cherche dans le champ, puis dans toute la fenêtre.
            var options = Named(Search(combo, ControlType.ListItem));
            if (options.Count == 0)
                options = Named(Search(TopLevel(combo), ControlType.ListItem));
            if (options.Count > 0 || DateTime.UtcNow >= deadline)
                return options;

            Thread.Sleep(PollInterval);
        }
    }

    private static List<AutomationElement> Named(List<AutomationElement> elements) =>
        [.. elements.Where(e => !string.IsNullOrWhiteSpace(e.Current.Name))];

    private static List<AutomationElement> Search(AutomationElement root, ControlType type) =>
        [.. root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type))
            .OfType<AutomationElement>()];

    private static AutomationElement TopLevel(AutomationElement element)
    {
        var walker = TreeWalker.ControlViewWalker;
        var current = element;
        for (int level = 0; level < 20; level++)
        {
            var parent = walker.GetParent(current);
            if (parent is null || Automation.Compare(parent, AutomationElement.RootElement))
                return current;

            current = parent;
        }

        return current;
    }

    private static bool Supports(AutomationElement element, AutomationPattern pattern) =>
        element.TryGetCurrentPattern(pattern, out _);

    private static ValuePattern? GetValuePattern(AutomationElement element) =>
        element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) ? (ValuePattern)pattern : null;

    private static int CountSpinners(AutomationElement element) =>
        element.FindAll(TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Spinner)).Count;

    /// <summary>
    /// Écrit puis relit : l'accessibilité accepte parfois l'écriture sans rien changer
    /// (c'est le cas des sélecteurs de date), et il ne faut pas annoncer un succès à tort.
    /// </summary>
    private static FillResult Write(ValuePattern value, AutomationElement element, string text)
    {
        try
        {
            value.SetValue(text);
            return GetValuePattern(element)?.Current.Value == text ? FillResult.Done : FillResult.Failed;
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return FillResult.Failed;
        }
    }

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
