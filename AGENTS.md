# Consignes du projet universel

- Travailler uniquement dans ce dossier. Ne pas modifier la version personnelle voisine ou son installation.
- Deux distributions : portable (Data à côté de l’exécutable) et installée (LocalAppData/LouControlCenterUniversal). Ne jamais charger les dossiers LouControlCenter de la version personnelle.
- Le portable ne crée aucune tâche permanente. L’installateur universel a un AppId, un dossier Program Files et une tâche de collecte distincts ; il inclut PawnIO signé et un démarrage Windows facultatif. Le collecteur SYSTEM ne lit aucune commande ni chemin provenant de la configuration utilisateur.
- Monitoring en lecture seule. Ne jamais appeler SetDefault / SetSoftware ni écrire les courbes BIOS/GPU. Lire docs/ARCHITECTURE.md.
- Correspondances utilisateur par fournisseur, carte, contrôleur, capteur et empreinte d’inventaire. Ne jamais réassocier d’après les RPM, le nom seul ou la position dans une liste.
- Ne jamais lancer deux écrivains LCD. Le mutex normal est partagé avec la version personnelle ; --preview et --review-ui n’envoient aucune donnée USB.
- Conserver les threads de rendu/transfert indépendants de WPF. Mesurer les frames uniques transmises pour les essais physiques, pas seulement la cadence demandée.
- Les essais temporaires ne doivent pas changer le délai sauvegardé. Un package ne doit contenir ni Data, ni Review, ni journaux ou identifiants de matériel personnels.
- Pour vérifier le package, tester l’exécutable autonome, et non uniquement un lancement via le SDK .NET.
