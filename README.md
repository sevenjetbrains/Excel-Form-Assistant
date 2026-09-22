# Excel Form Assistant

Petite application Windows (C# / .NET 8 / WPF) qui copie en un clic une donnée d'une ligne Excel pour la coller ensuite dans n'importe quel formulaire avec Ctrl+V.

## Avancement (ordre du cahier des charges)

- [x] 1. Lecture du fichier Excel et affichage du tableau
- [ ] 2. Choix de la feuille
- [ ] 3. Ligne active
- [ ] 4. Menu « Données Excel » et copie dans le presse-papiers
- [ ] 5. Raccourci global
- [ ] 6. Recherche
- [ ] 7. Rechargement
- [ ] 8. Mémorisation des paramètres

## Règles sur les données

| Cas | Règle |
|---|---|
| Numéros (0550123456) | Texte tel qu'affiché, zéros initiaux conservés |
| Dates | Toujours JJ/MM/AAAA (15/05/1993), jamais le numéro de série |
| Cellule vide | Affichée « — » |
| En-tête vide / en double | « Colonne 3 », « Nom (2) » |
| Fichier ouvert dans Excel | Lu quand même (lecture partagée) |
| Fichier original | Jamais modifié |

## Compiler et tester

Prérequis : SDK .NET 8.

```
dotnet build
dotnet test
dotnet run --project src/ExcelFormAssistant -- samples/exemple.xlsx
```

`samples/exemple.xlsx` est le fichier d'exemple du cahier des charges (BENALI, AMEUR).
