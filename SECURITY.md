# Sécurité

Le collecteur installé s’exécute avec les droits système pour accéder aux capteurs et à ETW. Il utilise uniquement des commandes et emplacements fixes protégés dans Program Files et ProgramData. Il ne doit jamais charger une commande, un script, une bibliothèque ou une adresse fournie par les réglages utilisateur.

Aucune écriture des courbes de ventilation, des réglages GPU ou du firmware LCD. Aucun mode non signé de PawnIO, aucune désactivation des protections de pilotes Windows.

Avant publication, activer les signalements privés de vulnérabilités dans les paramètres du dépôt GitHub. Utiliser ensuite l’onglet Security du dépôt pour transmettre un problème sensible. Ne jamais publier de clés, de fichiers de configuration personnels ou d’identifiants de machine.

Les signatures et empreintes des redistribuables tiers sont vérifiées avant distribution. LOU ne dispose pas encore d’un certificat de signature de code ; un avertissement Windows de réputation peut apparaître.
