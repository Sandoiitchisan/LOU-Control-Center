# Installation et premiers réglages

Windows 10 version 2004 (19041) ou Windows 11, x64. Un écran Thermalright utilisant un des profils JPEG reconnus est nécessaire pour l’affichage physique ; l’interface reste utilisable sans écran.

## Installer

Lancer le Setup, accepter les droits administrateur, choisir les raccourcis et éventuellement le démarrage Windows. Il installe LOU dans Program Files/LOU Control Center Universal, configure la collecte en lecture seule et ouvre l’application avec les droits ordinaires du compte. Un redémarrage peut être demandé si le composant matériel vient d’être installé.

L’assistant est autonome. Une connexion Internet reste nécessaire pour la météo et sa recherche de ville. La signature éditeur de LOU n’est pas encore disponible ; les empreintes SHA-256 servent à vérifier l’intégrité des fichiers, pas à remplacer une signature de confiance.

1. Fermer TRCC et toute autre version de LOU depuis la zone de notification.
2. Vérifier les appareils, orientations et pages dans **Écrans**. Le délai entre pages est indépendant pour chaque écran.
3. Dans **Capteurs**, associer radiateur, ventilateurs de boîtier et pompe aux capteurs confirmés par le câblage. Les RPM seuls ne permettent pas d’identifier un connecteur. Les températures et le GPU unique sont détectés automatiquement lorsque disponibles.
4. Chercher et choisir sa ville dans **Météo**, puis enregistrer.

## Désinstaller ou réparer

Relancer le Setup pour réparer les fichiers et recréer la tâche de collecte. Il conserve les réglages. La désinstallation depuis les applications Windows retire LOU, les raccourcis, son entrée de démarrage et sa tâche de collecte. Les réglages utilisateur et PawnIO sont conservés. PawnIO se désinstalle séparément si aucun autre logiciel ne l’utilise.

## Diagnostic

- Aucun écran : vérifier le câble USB interne, fermer TRCC, attendre la nouvelle détection. Les appareils `USB/MS_COMP_WINUSB` utilisent automatiquement WinUSB inclus dans Windows. Les variantes différentes peuvent nécessiter leur pilote constructeur. LOU ne change ni leur firmware ni la sécurité des pilotes Windows.
- Températures ou RPM absents : consulter Capteurs, puis `%ProgramData%/LouControlCenterUniversal/Sensors/installation.log`. Après une installation PawnIO, redémarrer Windows si demandé. Certains composants n’exposent aucune mesure ; LOU affiche une absence, jamais une valeur inventée.
- FPS absents : dans la version installée, vérifier que `LOU Control Center Universal - Capteurs` fonctionne dans le Planificateur de tâches. La mesure fonctionne via le collecteur, sans devoir exécuter l’interface en administrateur. Un jeu protégé ou une API non observée peut rester indisponible.
- Réglages : `%LocalAppData%/LouControlCenterUniversal`, avec sauvegarde `settings.json.bak`. Portable : `Data` à côté de l’exécutable.
- Une autre édition est ouverte : la quitter complètement avant de lancer celle-ci. Les deux versions ne doivent pas écrire simultanément sur les mêmes écrans.

## Portable

Extraire l’intégralité du ZIP dans un dossier accessible en écriture. Ne pas ajouter le fichier `installed.mode`. Le dossier contient le runtime .NET et les composants. La collecte avancée temporaire requiert une autorisation Windows ; aucun service LOU ni démarrage automatique n’est installé. Les images personnalisées restent dans Data/Backgrounds : copier ce dossier avec la configuration lors d’une migration manuelle.
