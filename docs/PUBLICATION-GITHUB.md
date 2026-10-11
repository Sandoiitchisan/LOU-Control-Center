# Préparer la page et le téléchargement

Dépôt cible : https://github.com/Sandoiitchisan/LOU-Control-Center

## Présentation du dépôt

Le README à la racine est la page de présentation. Les images dans docs/images doivent être publiées avec lui.

Description courte à coller dans le champ **About** :

> Une application Windows pour vos écrans Thermalright : système, météo, musique, jeux et ambiance. Plusieurs écrans, réglages indépendants et capteurs en lecture seule.

Topics suggérés : `thermalright`, `lcd`, `hardware-monitoring`, `windows`, `aio`, `system-monitor`, `csharp`, `dotnet`.

## Mettre le Setup en téléchargement

1. Publier les sources de ce dossier dans le dépôt cible, avec les licences et docs/images.
2. Dans **Releases**, choisir **Draft a new release**.
3. Créer le tag **v2.1.0** sur le commit contenant ces sources.
4. Utiliser le titre **LOU Control Center 2.1.0 — Windows x64** et le texte de docs/RELEASE-2.1.0.md.
5. Joindre les quatre fichiers de distribution : Setup EXE, ZIP portable, ZIP des sources et SHA256SUMS.txt.
6. Vérifier les pièces jointes, puis publier la release. Vérifier ensuite le bouton de téléchargement du README.

Les URL du README ciblent explicitement le tag v2.1.0 et les noms exacts de ces fichiers. Elles fonctionneront une fois les fichiers joints à la release publiée. Si le dépôt est privé, les visiteurs devront y avoir accès ; un téléchargement destiné à tout le monde nécessite un dépôt public.

Le Setup et le ZIP portable sont des pièces jointes de Release. Ne pas les ajouter à l’historique Git ni utiliser les archives automatiques « Source code » comme application à lancer.

Les fichiers de distribution ont été préparés dans `artifacts/publication-v2.1.0` de la copie de travail. Ce dossier est ignoré par Git.

Documentation : [créer une release](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository) et [liens directs vers les fichiers](https://docs.github.com/en/repositories/releasing-projects-on-github/linking-to-releases).
