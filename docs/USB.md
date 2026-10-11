# Protocoles USB et provenance

Références consultées pour l’interopérabilité, sans exécuter les logiciels ni redistribuer le firmware Thermalright :

| Source | Révision |
|---|---|
| https://github.com/Lexonight1/thermalright-trcc-linux | a73e510d48c8f3ba14421705c0e569410608cf0e |
| https://github.com/Abigor-DIY/Open-Trofeo-LCD | 9ecbf337f6c7e2fa5d0a67cb62022b454bdb115e |
| https://github.com/0iroN/trvision | bc88670ac6b00a569d7228b083ef4badae987567 |
| https://github.com/rejeb/thermalright-lcd-control | 35b0b29001a31ca80b4a9daa7723029620a8ff6e |

LY (0416:5408) : handshake 2 048 octets, magic validé, géométrie issue du PM/SUB et du timing quand présent. JPEG inférieur à 450 000 octets, fragments de 496 octets dans blocs 512, padding par quatre blocs, bursts 4096/2048, ACK après frame.

ChiZhu (87AD:70DB) : handshake 64 octets, PM à l’offset 24, SUB à 36, en-tête image 64 octets, dimensions et format, taille à l’offset 60. Transfert 16 Kio et paquet vide si alignement USB. Envoi continu nécessaire sur certains écrans carrés.

Découverte par PnP et descripteurs WinUSB, aucune substitution de pilote. Un modèle inconnu, une géométrie incohérente ou un format RGB565 est refusé. Aucun envoi de commande de firmware, logo permanent ou contrôle de ventilation.

Le profil initial est détecté automatiquement ; l’utilisateur peut vérifier la mire et choisir l’orientation. Les reconnexions exigent la même identité PnP et la même géométrie native. Les deux familles sont gérées par des sessions indépendantes, avec transfert sérialisé par appareil.

Les profils JPEG de la table protocolaire peuvent être décodés, mais seuls les formats 1920 × 462 et 480 × 480 ont été observés sur le matériel de référence. Les autres variantes nécessitent une validation physique dédiée. Un test par fermeture/réouverture de handle ne prouve pas la reprise après retrait de câble ou veille.
