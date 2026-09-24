namespace ExcelFormAssistant.Tests;

/// <summary>
/// Le presse-papiers est unique pour toute la machine : les classes qui l'utilisent ne
/// doivent pas tourner en parallèle, sinon l'une lit ce que l'autre vient d'y écrire.
/// </summary>
[CollectionDefinition("Presse-papiers")]
public sealed class ClipboardCollection;
