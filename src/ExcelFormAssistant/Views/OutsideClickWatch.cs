namespace ExcelFormAssistant.Views;

/// <summary>
/// Décide quand un menu flottant, qui n'a pas le focus, doit se fermer : l'utilisateur est
/// passé à une autre fenêtre, ou il a cliqué ailleurs.
///
/// La surveillance ne s'arme qu'une fois tous les boutons relâchés. Sans cela, le clic qui
/// vient d'ouvrir le menu — ou celui que l'application envoie pour donner le focus au champ —
/// est encore vu comme enfoncé, et le menu se referme aussitôt qu'il s'ouvre.
/// </summary>
internal sealed class OutsideClickWatch
{
    private bool _armed;

    public bool ShouldDismiss(bool foregroundChanged, bool anyButtonDown, bool cursorInsideMenu)
    {
        if (foregroundChanged)
            return true;

        if (!_armed)
        {
            _armed = !anyButtonDown;
            return false;
        }

        return anyButtonDown && !cursorInsideMenu;
    }
}
