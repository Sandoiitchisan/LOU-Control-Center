<div align="center">

# LOU Control Center

**Votre PC. Vos écrans. Vos informations.**

Une application Windows pour transformer les écrans de votre boîtier et de votre AiO Thermalright en tableaux de bord personnalisés.

![Windows 10 et 11 x64](https://img.shields.io/badge/Windows-10%20%7C%2011%20x64-0078D4?style=flat-square)
![Version 2.1.0](https://img.shields.io/badge/version-2.1.0-68E4CF?style=flat-square)
[![Licence GPL-3.0](https://img.shields.io/badge/licence-GPL--3.0-9B5CFF?style=flat-square)](LICENSE)

[![Télécharger pour Windows — Setup autonome x64](docs/images/telecharger.svg)](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.0/LOU-Control-Center-Setup-2.1.0-win-x64.exe)

[Version portable](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.0/LOU-Control-Center-Portable-2.1.0-win-x64.zip) · [Notes de version](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/tag/v2.1.0) · [Guide d’installation](docs/INSTALLATION.md)

</div>

![Page Système de LOU Control Center sur un écran panoramique](docs/images/systeme.png)

*Aperçus produits par l’application avec des données de démonstration.*

## Vos informations, à portée de regard

La température du processeur, la musique en cours, la météo ou les performances d’un jeu : LOU Control Center rassemble ces informations sur les petits écrans déjà présents dans votre PC.

Utilisez un écran AiO seul, un grand écran de boîtier ou les deux ensemble. Choisissez les pages à afficher et leur rythme de défilement. Chaque écran garde ses propres réglages, dans une interface commune.

## Cinq pages pour votre quotidien

| Page | Ce qu’elle affiche |
|---|---|
| **Système** | Charge CPU, GPU et RAM, températures, vitesses des ventilateurs et de la pompe lorsqu’elles sont disponibles, débits réseau en Mo/s. |
| **Météo** | Conditions actuelles et prévisions. Recherchez votre ville, choisissez le bon résultat : aucune clé API à renseigner. |
| **Musique** | Titre, artiste, progression et pochette lorsque votre lecteur les transmet à Windows. |
| **Jeux** | Jeu détecté, cadence de présentation mesurée avec PresentMon, charge GPU et taille de la fenêtre du jeu. |
| **Ambiance** | Horloge et décors pour un affichage plus discret. |

![Page Musique de LOU Control Center](docs/images/musique.png)

## Un petit écran peut aussi tout afficher

Les cinq pages disposent d’une présentation adaptée aux écrans compacts. Vous n’avez que l’écran de votre AiO ? Vous pouvez y retrouver le système, la météo, la musique, les jeux et l’ambiance.

Une vue dédiée à la température CPU et au refroidissement est également disponible.

<p align="center">
  <img src="docs/images/meteo-compacte.png" alt="Météo sur un écran carré" width="320" />
  <img src="docs/images/musique-compacte.png" alt="Musique sur un écran carré" width="320" />
</p>

## Un affichage qui s’adapte à votre configuration

- **Détection des écrans compatibles**, avec orientation réglable et mire de vérification.
- **Pages, ordre, délai et transitions indépendants** pour chaque écran.
- **Couleurs, intensité des animations, mode nuit et images personnalisées**.
- **Températures et ventilateurs AMD/NVIDIA**, selon les mesures exposées par le matériel et ses pilotes.
- **Associations de capteurs configurables** : vous choisissez quelle mesure correspond au radiateur, au boîtier ou à la pompe.
- **Monitoring en lecture seule** : vos courbes de ventilation restent gérées par le BIOS et vos logiciels habituels.
- **Météo sans clé API**, avec conservation de la dernière prévision connue en cas de coupure réseau.

## Télécharger et démarrer

### Installation Windows — recommandée

[Télécharger le Setup autonome 2.1.0 pour Windows x64](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.0/LOU-Control-Center-Setup-2.1.0-win-x64.exe)

**Le Setup contient les composants nécessaires** : moteur .NET, outils de mesure, installateur officiel signé PawnIO pour les capteurs et illustrations. L’installation de ces éléments ne demande aucun téléchargement supplémentaire. Internet reste nécessaire pour la météo.

1. Fermez TRCC et les autres logiciels qui utilisent vos écrans Thermalright.
2. Lancez le Setup et acceptez l’autorisation Windows. Le démarrage avec Windows est proposé en option.
3. Ouvrez LOU Control Center et vérifiez vos appareils dans **Écrans**.
4. Dans **Capteurs**, associez les ventilateurs et la pompe aux mesures correspondant à votre câblage.
5. Dans **Météo**, cherchez et choisissez votre ville, puis enregistrez.

L’application s’utilise ensuite avec les droits ordinaires de votre compte. La collecte avancée est assurée par le composant installé en arrière-plan.

> L’installateur LOU n’a pas encore de signature éditeur : Windows peut afficher un avertissement de réputation. Les composants tiers inclus sont leurs versions officielles signées. Les empreintes des fichiers sont disponibles dans [SHA256SUMS.txt](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.0/SHA256SUMS.txt).

### Version portable

[Télécharger le ZIP portable 2.1.0](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.0/LOU-Control-Center-Portable-2.1.0-win-x64.zip), l’extraire entièrement dans un dossier accessible en écriture, puis ouvrir `Lou.ControlCenter.exe`.

Les réglages et les images restent dans le dossier `Data`. LOU ne crée pas de tâche permanente dans ce mode. La lecture avancée des capteurs peut demander une autorisation Windows et l’installation de PawnIO s’il est absent. Une fois activée, cette collecte temporaire s’arrête avec l’application.

## Compatibilité

**Windows 10 version 2004 minimum (19041) ou Windows 11, x64.**

| Élément | Prise en charge actuelle |
|---|---|
| Écrans Thermalright | Familles USB **LY** (`0416:5408`) et **ChiZhu** (`87AD:70DB`), avec profils JPEG reconnus. |
| Formats vérifiés sur matériel réel | **1920 × 462** et **480 × 480**, utilisés simultanément. |
| Autres variantes d’écran | Les profils reconnus peuvent être détectés ; leur fonctionnement doit être confirmé sur le modèle concerné. |
| Températures et RPM | Mesures fournies par LibreHardwareMonitor, avec un complément AMD ADL. Leur disponibilité dépend du matériel. |
| NVIDIA | Prise en charge par la bibliothèque ; validation sur une carte physique encore attendue. |
| Lecteurs musicaux | Applications qui exposent leurs informations via les sessions multimédias Windows. |

La marque Thermalright seule ne garantit pas la compatibilité de chaque modèle. Les formats inconnus ou RGB565 sont signalés comme non pris en charge. Sur les appareils qui déclarent la compatibilité WinUSB, Windows fournit automatiquement le pilote ; les autres variantes peuvent nécessiter leur pilote constructeur.

La détection des jeux repose sur leur activité graphique et des indices logiciels ; elle peut nécessiter une exception utilisateur. Les FPS représentent la cadence des présentations, pas une distinction entre images rendues et générées. La résolution indiquée correspond à la fenêtre. FSR, DLSS, HDR et génération d’images ne sont pas détectés.

[Compatibilité USB et protocoles](docs/USB.md) · [Installation et dépannage](docs/INSTALLATION.md)

## État du projet

La version 2.1.0 a passé **109 tests automatisés**. L’installation, la réparation, le lancement sans droits administrateur et la désinstallation ont été vérifiés sur le PC de référence. Lors d’un essai simultané, les deux écrans ont atteint environ **24 images/s** et **10 images/s** respectivement.

Ces résultats concernent le matériel testé. Les essais sur Windows vierge, d’autres cartes et d’autres écrans permettront d’élargir la validation. Les cadences dépendent du modèle et des réglages.

Vous testez un autre écran ? Un retour dans les [Issues](https://github.com/Sandoiitchisan/LOU-Control-Center/issues), avec son modèle exact et le comportement observé, aidera à documenter sa compatibilité. Pensez à retirer les identifiants personnels de vos captures et journaux.

[Compte rendu de validation](docs/VALIDATION.md)

## Développer et contribuer

Le projet utilise **C# / .NET 10, WPF et SkiaSharp**.

```powershell
# Compiler, tester et créer le portable
./scripts/build.ps1

# Créer également le Setup, avec Inno Setup 6 installé
./scripts/build.ps1 -Installer
```

Le SDK .NET est nécessaire pour compiler les sources, pas pour utiliser les packages publiés. Le workflow GitHub produit les packages Windows et exécute les tests.

[Contribuer](CONTRIBUTING.md) · [Architecture](docs/ARCHITECTURE.md) · [Sécurité](SECURITY.md)

## Licence et crédits

LOU Control Center est un projet indépendant, distribué sous **[GPL-3.0](LICENSE)**, sans affiliation avec Thermalright.

Il s’appuie notamment sur LibreHardwareMonitor, PresentMon, PawnIO et SkiaSharp. La météo est fournie par [Open-Meteo](https://open-meteo.com/), avec la recherche de villes GeoNames ; l’accès public sans clé est destiné aux usages non commerciaux dans les quotas du service. Les données météo sont attribuées sous CC BY 4.0.

[Licences et attributions complètes](THIRD_PARTY_NOTICES.md) · [Origine des illustrations](docs/ASSETS.md)
