using System.Windows;
using System.Windows.Automation;
using Condition = System.Windows.Automation.Condition;
using ExcelFormAssistant.Models;

namespace ExcelFormAssistant.Services;

/// <summary>
/// Liste les champs de formulaire d'une zone de l'écran grâce à UI Automation, l'interface
/// d'accessibilité de Windows (celle des lecteurs d'écran). Rien n'est modifié : seuls le
/// type, le libellé et l'état de chaque champ sont lus — jamais la valeur qu'il contient.
///
/// À appeler hors du thread d'interface : les requêtes UI Automation peuvent prendre du
/// temps sur une grande page, et s'adresser à ses propres fenêtres depuis ce thread bloque.
/// </summary>
public sealed class FormFieldScanner
{
    /// <summary>Autour de la zone, on cherche aussi les libellés qui la débordent un peu.</summary>
    private const double LabelSearchMargin = 320;

    private static readonly Condition FieldCondition = new OrCondition(
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.RadioButton));

    private static readonly Condition TextCondition =
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text);

    /// <param name="zone">Zone sélectionnée, en pixels écran.</param>
    /// <returns>Champs dont le centre est dans la zone, de haut en bas puis de gauche à droite.</returns>
    public IReadOnlyList<DetectedField> Scan(Rect zone)
    {
        var window = TopLevelWindowAt(new Point(zone.X + zone.Width / 2, zone.Y + zone.Height / 2));
        if (window is null)
            return [];

        var fields = FindFields(window, zone);
        if (fields.Count == 0)
        {
            // Chrome et Edge ne construisent leur arbre d'accessibilité qu'à la première
            // demande d'un outil d'accessibilité : la deuxième lecture est la bonne.
            Thread.Sleep(600);
            fields = FindFields(window, zone);
        }
        return fields;
    }

    private static List<DetectedField> FindFields(AutomationElement window, Rect zone)
    {
        var labelArea = zone;
        labelArea.Inflate(LabelSearchMargin, LabelSearchMargin / 3);
        var texts = ReadTexts(window, labelArea);

        var fields = new List<DetectedField>();
        foreach (AutomationElement element in window.FindAll(TreeScope.Descendants, FieldCondition))
        {
            try
            {
                var info = element.Current;
                var bounds = info.BoundingRectangle;
                if (bounds.IsEmpty || info.IsOffscreen || !zone.Contains(Center(bounds)))
                    continue;
                // Une liste déroulante modifiable contient sa propre zone de texte : on ne garde que la liste.
                if (info.ControlType == ControlType.Edit && IsInsideComboBox(element))
                    continue;

                var (label, source) = FindLabel(element, info, bounds, texts);
                var (hasValue, isReadOnly) = ReadState(element, info);
                fields.Add(new DetectedField(KindOf(info), label, source, bounds, info.IsPassword, isReadOnly, hasValue));
            }
            catch (ElementNotAvailableException)
            {
                // Le champ a disparu pendant la lecture (page qui se met à jour) : on l'ignore.
            }
        }

        return [.. fields.OrderBy(f => Math.Round(f.Bounds.Top / 10)).ThenBy(f => f.Bounds.Left)];
    }

    private static (string Label, LabelSource Source) FindLabel(AutomationElement element,
        AutomationElement.AutomationElementInformation info, Rect bounds, List<(string, Rect)> texts)
    {
        if (!string.IsNullOrWhiteSpace(info.Name))
            return (info.Name.Trim(), LabelSource.AccessibleName);

        if (info.LabeledBy is AutomationElement labeledBy && !string.IsNullOrWhiteSpace(labeledBy.Current.Name))
            return (labeledBy.Current.Name.Trim(), LabelSource.LabeledBy);

        if (!string.IsNullOrWhiteSpace(info.HelpText))
            return (info.HelpText.Trim(), LabelSource.HelpText);

        if (LabelLocator.FindNearest(bounds, texts) is string nearby)
            return (nearby, LabelSource.NearbyText);

        return (string.Empty, LabelSource.None);
    }

    /// <summary>Le champ contient-il quelque chose, et accepte-t-il la saisie ? La valeur n'est pas conservée.</summary>
    private static (bool HasValue, bool IsReadOnly) ReadState(AutomationElement element,
        AutomationElement.AutomationElementInformation info)
    {
        if (info.ControlType == ControlType.CheckBox || info.ControlType == ControlType.RadioButton)
            return (false, !info.IsEnabled);
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && pattern is ValuePattern value)
        {
            bool hasValue = !info.IsPassword && !string.IsNullOrEmpty(value.Current.Value);
            return (hasValue, value.Current.IsReadOnly || !info.IsEnabled);
        }
        return (false, !info.IsEnabled);
    }

    private static List<(string, Rect)> ReadTexts(AutomationElement window, Rect area)
    {
        var texts = new List<(string, Rect)>();
        foreach (AutomationElement element in window.FindAll(TreeScope.Descendants, TextCondition))
        {
            try
            {
                var info = element.Current;
                if (!info.IsOffscreen && !info.BoundingRectangle.IsEmpty && area.IntersectsWith(info.BoundingRectangle))
                    texts.Add((info.Name, info.BoundingRectangle));
            }
            catch (ElementNotAvailableException)
            {
            }
        }
        return texts;
    }

    private static string KindOf(AutomationElement.AutomationElementInformation info)
    {
        if (info.ControlType == ControlType.ComboBox) return "Liste déroulante";
        if (info.ControlType == ControlType.CheckBox) return "Case à cocher";
        if (info.ControlType == ControlType.RadioButton) return "Bouton radio";
        return info.IsPassword ? "Mot de passe" : "Zone de texte";
    }

    private static bool IsInsideComboBox(AutomationElement element) =>
        TreeWalker.ControlViewWalker.GetParent(element) is AutomationElement parent
        && parent.Current.ControlType == ControlType.ComboBox;

    /// <summary>Fenêtre principale de l'application affichée sous ce point.</summary>
    private static AutomationElement? TopLevelWindowAt(Point point)
    {
        AutomationElement? element;
        try
        {
            element = AutomationElement.FromPoint(point);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }

        var walker = TreeWalker.ControlViewWalker;
        while (element is not null)
        {
            var parent = walker.GetParent(element);
            if (parent is null || parent == AutomationElement.RootElement)
                return element;
            element = parent;
        }
        return null;
    }

    private static Point Center(Rect r) => new(r.X + r.Width / 2, r.Y + r.Height / 2);
}
