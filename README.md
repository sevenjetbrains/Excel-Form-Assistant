# Excel Form Assistant

Petite application Windows (C# / .NET 8 / WPF) qui colle en un clic une donnée d'une ligne Excel dans n'importe quel formulaire.

## Avancement (ordre du cahier des charges)

- [x] 1. Lecture du fichier Excel et affichage du tableau
- [x] 2. Choix de la feuille
- [x] 3. Ligne active
- [x] 4. Menu « Données Excel » et copie dans le presse-papiers
- [x] 5. Raccourci global
- [x] 6. Recherche
- [x] 7. Rechargement
- [x] 8. Mémorisation des paramètres

Au-delà du cahier des charges :

- [x] 9. Coller au clic droit sur le champ

## Coller au clic droit

**Maj + clic droit** sur un champ, dans n'importe quelle application, ouvre le menu
« Données Excel » à l'endroit du curseur, avec les colonnes et les valeurs de la ligne
active (`Nom : BENALI`, `Téléphone : 0550123456`…). Un clic sur une ligne du menu colle la
valeur dans le champ.

Le clic droit **seul** n'est pas touché : le menu contextuel habituel de l'application
s'ouvre comme d'habitude. Seul le clic droit avec Maj est détourné, et dans ce cas
l'application sous le curseur ne le reçoit pas — c'est pourquoi son menu n'apparaît pas.

Ce qui se passe exactement au moment du collage :

1. le clic droit est avalé, donc le champ n'a pas reçu le focus : un clic gauche le lui donne ;
2. la valeur est **toujours** copiée dans le presse-papiers, même si le collage échoue ;
3. Ctrl+V est envoyé à la fenêtre qui avait le focus à l'ouverture du menu, après avoir
   relâché Maj (sinon le formulaire recevrait Ctrl+Maj+V, qui ne colle pas partout) ;
4. si l'utilisateur a changé de fenêtre entre-temps, **rien n'est envoyé** : la bulle indique
   « copié — Ctrl+V pour coller », pour ne jamais écrire ailleurs que là où il l'attend.

Une entrée dans le menu contextuel de l'application elle-même n'est pas possible : chaque
logiciel dessine son propre menu, et Windows n'offre aucun moyen d'y ajouter une ligne —
les shell extensions ne concernent que les fichiers et dossiers de l'Explorateur.

## Raccourci global

`Ctrl+Maj+E` depuis n'importe quelle application ouvre le même menu près de la souris, sans
quitter le clavier. Le collage suit les mêmes règles que ci-dessus.

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

## Mémorisation des paramètres

À la fermeture, l'application retient dans
`%AppData%\ExcelFormAssistant\parametres.json` le dernier fichier ouvert, la feuille
affichée, et la position et la taille de la fenêtre. Au lancement suivant, tout est remis
en place : le fichier est rouvert sur la même feuille.

Rien de tout cela ne peut empêcher l'application de démarrer :

- fichier de paramètres absent, vide ou abîmé → on repart des valeurs par défaut ;
- dernier fichier déplacé ou supprimé → l'application s'ouvre vide, sans message d'erreur ;
- écran débranché ou résolution changée depuis la dernière fois → la fenêtre revient
  centrée plutôt que hors de l'écran ;
- paramètres impossibles à enregistrer (dossier en lecture seule, disque plein) → la
  fermeture se fait quand même.

Un fichier passé en argument reste prioritaire sur le fichier mémorisé.

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
