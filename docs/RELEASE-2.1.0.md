# LOU Control Center 2.1.0 — Vos informations, sur vos écrans Thermalright

Transformez les écrans de votre boîtier et de votre AiO Thermalright en tableaux de bord : système, météo, musique, jeux et ambiance, dans une seule application Windows.

## Télécharger

**Pour la plupart des utilisateurs : [télécharger le Setup Windows x64](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.0/LOU-Control-Center-Setup-2.1.0-win-x64.exe).**

Le Setup est **autonome** : le moteur .NET, les outils de mesure, le composant officiel de lecture des capteurs et les illustrations sont inclus. Il installe l’application, ses raccourcis et la collecte des capteurs ; le démarrage avec Windows est facultatif. Internet reste nécessaire pour rechercher une ville et actualiser la météo.

Vous préférez conserver l’application dans un dossier ? [Télécharger la version portable](https://github.com/Sandoiitchisan/LOU-Control-Center/releases/download/v2.1.0/LOU-Control-Center-Portable-2.1.0-win-x64.zip), extraire entièrement le ZIP, puis ouvrir `Lou.ControlCenter.exe`.

## Ce que vous pouvez afficher

- **Système** : charge CPU/GPU/RAM, températures, débits réseau en Mo/s et vitesses de ventilation disponibles.
- **Météo** : conditions actuelles et prévisions, à partir de votre ville, sans clé API.
- **Musique** : lecture en cours et informations transmises par les lecteurs compatibles Windows.
- **Jeux** : détection de l’activité de jeu et cadence de présentation via PresentMon.
- **Ambiance** : horloge et décors pour un affichage plus discret.

Les cinq pages existent sur les écrans panoramiques et compacts. Chaque écran conserve son choix de pages, son délai, ses transitions et son orientation. Une vue dédiée au refroidissement est également disponible.

## Première utilisation

1. Fermez TRCC ou tout autre logiciel qui utilise les mêmes écrans.
2. Installez et ouvrez LOU Control Center.
3. Vérifiez vos appareils et leur orientation dans **Écrans**.
4. Associez vos ventilateurs et votre pompe à leurs capteurs dans **Capteurs**.
5. Choisissez votre ville dans **Météo**, puis enregistrez.

Le monitoring reste en lecture seule : vos courbes de ventilation restent gérées par le BIOS et les logiciels habituels.

## Compatibilité et état de validation

Windows 10 version 2004 minimum ou Windows 11, x64. Les familles Thermalright LY et ChiZhu avec profils JPEG reconnus sont prises en charge. Des essais physiques ont été réalisés simultanément sur des écrans 1920 × 462 et 480 × 480. Les autres modèles nécessitent une validation ; la marque seule ne garantit pas la compatibilité.

109 tests automatisés passent. Installation, réparation, lancement sans droits administrateur et désinstallation ont été vérifiés sur le PC de référence. L’installation sur Windows vierge reste à valider. La prise en charge NVIDIA existe via LibreHardwareMonitor, mais n’a pas encore été testée sur une carte physique.

L’installateur LOU n’est pas encore signé avec un certificat éditeur : Windows peut afficher un avertissement de réputation. Les redistribuables tiers sont leurs versions officielles signées. Les empreintes des téléchargements figurent dans `SHA256SUMS.txt`.

L’accès météo Open-Meteo sans clé est destiné aux usages non commerciaux, dans les quotas du service.

## Fichiers de cette version

| Fichier | Utilisation |
|---|---|
| `LOU-Control-Center-Setup-2.1.0-win-x64.exe` | Installation Windows recommandée |
| `LOU-Control-Center-Portable-2.1.0-win-x64.zip` | Application à extraire dans un dossier |
| `LOU-Control-Center-Sources-2.1.0.zip` | Sources du projet et licences |
| `SHA256SUMS.txt` | Empreintes de vérification |

Projet libre sous GPL-3.0, indépendant de Thermalright. Les retours accompagnés du modèle exact de votre écran sont les bienvenus dans les Issues.
