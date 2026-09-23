# Excel Form Assistant

Petite application Windows (C# / .NET 8 / WPF) qui copie en un clic une donnée d'une ligne Excel pour la coller ensuite dans n'importe quel formulaire avec Ctrl+V.

## Avancement (ordre du cahier des charges)

- [x] 1. Lecture du fichier Excel et affichage du tableau
- [x] 2. Choix de la feuille
- [x] 3. Ligne active
- [x] 4. Menu « Données Excel » et copie dans le presse-papiers
- [x] 5. Raccourci global
- [x] 6. Recherche
- [x] 7. Rechargement
- [ ] 8. Mémorisation des paramètres

## Raccourci global

`Ctrl+Maj+E` depuis n'importe quelle application ouvre le menu « Données Excel » de la ligne
active, près de la souris : pas besoin de revenir à la fenêtre du logiciel.

Si un autre logiciel utilise déjà la combinaison, la suivante de la liste est prise
(`Ctrl+Maj+D`, `Ctrl+Alt+E`, `Ctrl+Alt+D`, `Ctrl+Maj+F12`). Le raccourci réellement actif est
toujours affiché dans la barre d'état.

## Recherche

`Ctrl+F` amène dans la zone « Rechercher », qui filtre le tableau à chaque frappe :

- majuscules et accents ignorés : `benaissa` trouve `Benaïssa` ;
- chaque mot tapé doit se trouver quelque part dans la ligne, dans n'importe quel ordre :
  `benali ahmed` retrouve la ligne même si le nom et le prénom sont dans deux colonnes ;
- la recherche porte sur le texte affiché : `15/05/1993` trouve la date, `0550` le téléphone ;
- `Entrée` passe au tableau sur la première ligne trouvée (une deuxième `Entrée` l'active) ;
- `Échap` ou la croix efface la recherche.

La ligne active reste la même pendant une recherche, même si elle est filtrée : le menu
« Données Excel » continue de proposer ses valeurs.

## Rechargement

Le bouton « Recharger » ou `F5` relit le fichier : ce qui a été modifié, ajouté ou supprimé
dans Excel entre-temps apparaît dans le tableau. La feuille, la recherche en cours et la
ligne active sont conservées — la ligne active est retrouvée par son numéro de ligne Excel,
donc le menu « Données Excel » propose aussitôt les valeurs à jour. Si cette ligne a été
supprimée du fichier, elle est simplement oubliée.

Le fichier peut rester ouvert dans Excel pendant le rechargement. S'il est devenu illisible
(déplacé, supprimé), le message d'erreur s'affiche et le tableau garde ce qu'il montrait.

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
