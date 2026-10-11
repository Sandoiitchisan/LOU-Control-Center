# Architecture

Le projet est indépendant de l’édition personnelle. Aucune connexion serveur, ville, adresse SSH, association matérielle ou série USB personnelle n’est incluse.

- Core : configuration JSON portable, cinq pages, profils par écran et ordonnanceurs indépendants.
- Providers : un DataHub partagé collecte les informations du PC, des médias, du jeu et de la météo. LibreHardwareMonitor 0.9.6 fournit températures et ventilateurs AMD/NVIDIA/Intel, avec AMD ADL en complément. Les mesures absentes ne valent jamais zéro.
- Devices : découverte PnP limitée aux familles USB reconnues, handshake LY/ChiZhu, session sérialisée. Le profil persistant est comparé à chaque reconnexion. Un changement exige une réinitialisation de la détection.
- Rendering : composition panoramique existante et cinq compositions compactes. Les angles 90/270 changent les dimensions logiques avant rotation physique. Une vue de refroidissement optionnelle est disponible.
- App : une paire de threads rendu/transport par écran, attente haute résolution annulable, dernière frame uniquement. L’interface prélève l’aperçu à 5 Hz ; elle ne bloque pas les threads de rendu. Les limites demandées ne constituent pas une mesure de FPS réelle.

## Identité des mesures

Une association comprend fournisseur, carte mère, identifiant du contrôleur, identifiant du capteur, nom du matériel et empreinte de topologie. Cette empreinte est dérivée de l’inventaire trié des contrôleurs et des identifiants physiques GPU exposés par la bibliothèque ; elle ne dépend ni des RPM ni de l’ordre d’énumération. Un changement d’inventaire bloque les associations plutôt que de les réaffecter. Les noms de carte mère/Super I/O ne constituent pas une identification des connecteurs : l’utilisateur garde la responsabilité de confirmer le câblage dans le BIOS ou une documentation fiable.

Les GPU de même modèle sont distingués par DeviceId lorsque la bibliothèque l’expose. Si un fabricant n’expose pas d’identité physique stable, l’application ne prétend pas pouvoir reconnaître un remplacement indiscernable ; l’association reste configurable et doit être vérifiée après un changement matériel.

Les paramètres matériels de LibreHardwareMonitor sont fournis par ReadOnlyHardwareSettings : aucun fichier de réglage externe n’est chargé, les contrôles restent Undefined. L’application n’appelle aucune méthode SetDefault / SetSoftware d’un contrôleur matériel.

## Isolation

En mode portable, Data est résolu par rapport à l’exécutable, jamais dans le LocalAppData de la version personnelle. La collecte avancée écrit uniquement dans ce dossier et s’arrête avec son parent, dont le PID et l’heure de démarrage sont vérifiés. Le mutex d’affichage est partagé avec l’édition personnelle pour empêcher deux écrivains LCD. Le mode aperçu possède son propre mutex et n’ouvre aucun transport.

La fermeture par la croix peut réduire dans la zone de notification ; Quitter arrête les boucles, libère les transports et coupe PresentMon. Aucun démarrage Windows ou service permanent n’est créé par la version portable.

## Météo

Recherche HTTPS explicite par nom via geocoding-api.open-meteo.com. L’utilisateur choisit une ville avec région/pays ; seules les coordonnées de ce choix sont utilisées en interne. L’API forecast est appelée toutes les 20 minutes avec cache local, délai de nouvelle tentative et respect de Retry-After. Attribution Open-Meteo et GeoNames. Aucun compte ni clé API.

## Installation Windows

Le marqueur `installed.mode`, ajouté uniquement par Inno Setup, sélectionne le stockage `%LocalAppData%/LouControlCenterUniversal`. Les chemins relatifs Data/Backgrounds sont résolus contre cette racine ; le répertoire Program Files reste immuable. Le portable conserve son stockage et son collecteur temporaires.

Une tâche SYSTEM démarre le même exécutable protégé dans Program Files avec `--sensor-collector --machine`. Sa commande est fixe et ne charge aucune configuration utilisateur. Le collecteur écrit des instantanés bornés dans ProgramData/LouControlCenterUniversal/Sensors, accessible en lecture seule aux utilisateurs standards. Il lit les capteurs et exécute PresentMon dans un job Windows qui arrête le processus enfant quand le collecteur disparaît. La session ETW porte un nom spécifique à cette édition. Aucun groupe de sécurité du compte utilisateur n’est modifié. Les mesures des présentations ne contiennent ni nom de fenêtre ni contenu affiché et deviennent invalides après cinq secondes.

L’installateur et sa désinstallation arrêtent uniquement cette tâche et cette session. L’événement de fermeture de l’interface est spécifique à l’édition universelle ; le mutex USB reste partagé avec l’édition personnelle. Les paramètres, données et tâches de cette dernière restent indépendants.
