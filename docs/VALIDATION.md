# Validation

## Distribution 2.1.0 — 11 octobre 2026

- 109 tests automatisés réussis ; build Release sans avertissement. Reconstruction depuis une copie de sources isolée réussie.
- ZIP autonome : 517 entrées, aucune configuration, trace personnelle ni capture. Exécutable testé après extraction et depuis un autre répertoire de travail ; aperçu à zéro frame USB.
- Installation native via le Setup EXE : exit 0, raccourcis, collecte SYSTEM active, 38 mesures réelles, capture PresentMon active. Interface lancée avec un compte non élevé.
- Essai simultané des deux écrans : grand LCD 23,99 transmissions/s, 23,22 images distinctes/s ; AiO 10,01 transmissions et images distinctes/s. Les cinq pages ont été vues sur chaque écran. Trois blocages de l’interface de 250 ms ont été introduits.
- Réparation : configuration conservée, reprise des capteurs et FPS, retrait du démarrage automatique lorsque décoché.
- Démarrage avec --tray : fenêtre masquée et arrêt par --shutdown, exit 0, processus non administrateur.
- Désinstallation : exit 0, exécutable et tâche retirés, aucun processus universel ni session ETW résiduel ; réglages et PawnIO partagés conservés. L’exécutable de l’édition personnelle est inchangé.
- Le test a été effectué sur un PC déjà équipé de PawnIO 2.2.0 et de WinUSB. Le chemin d’installation initiale de PawnIO et l’installation sur un Windows vierge restent à valider dans cet environnement. Les signatures des redistribuables sont vérifiées et leurs installateurs officiels sont inclus.
- Le workflow GitHub est préparé ; aucune exécution distante ni publication n’a été effectuée.

## Version portable 2.0.0

Vérifications du 11 octobre 2026 :

- Compilation Release sans avertissement et 106 tests automatisés réussis.
- Rendu des cinq pages avec données simulées et indisponibles, aux formats 1920 × 462, 480 × 480, 320 × 240 et portrait ; inspection visuelle des pages carrées et de l’interface.
- Interface en six sections, fenêtre normale et taille minimale, réglages propres à chaque écran. Un changement de délai du premier écran ne modifie pas le second ; en aperçu, la sauvegarde utilise un fichier distinct et zéro frame USB est envoyée.
- Recherche de Lyon avec désambiguïsation France/États-Unis et requête météo Open-Meteo réelles, sans clé.
- Essai physique simultané d’environ 36 secondes : les cinq pages passent sur les deux écrans détectés automatiquement. Grand écran 1920 × 462 : 23,98 transmissions/s et 23,79 images distinctes/s. AiO 480 × 480 : 10,01 transmissions/s et 9,93 images distinctes/s. Trois blocages WPF de 250 ms ont été introduits pendant l’essai ; les transports sont indépendants de l’interface.
- Le délai temporaire du test (3 secondes) n’a pas été sauvegardé. Les deux profils sont restés à 15 secondes après l’essai. Aucune association personnelle de capteur n’a été importée.
- Les traces matérielles et les numéros de série des essais restent dans les dossiers de contrôle locaux, exclus du ZIP.

Limites : il s’agit d’un essai court, pas d’une preuve d’endurance sur plusieurs jours, de débranchement physique ou de reprise après veille. La nouvelle collecte NVIDIA et les autres modèles de cartes mères/écrans demandent des essais sur leur matériel réel. Les tests automatisés vérifient les identités et l’absence de fausses valeurs, sans remplacer ces essais. Les RPM doivent être associés par l’utilisateur au câblage de sa propre machine.
