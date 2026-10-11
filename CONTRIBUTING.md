# Contribuer

Installer le SDK .NET 10 et cloner le dépôt sur Windows x64. Les redistribuables officiels et leurs licences dans tools sont conservés sans modification ; leurs empreintes sont vérifiées lors du build.

```powershell
./scripts/build.ps1
# Avec Inno Setup 6 pour produire également le Setup :
./scripts/build.ps1 -Installer
```

Lire AGENTS.md et docs/ARCHITECTURE.md. Le monitoring des ventilateurs est strictement en lecture seule. Toute nouvelle famille d’écran doit avoir un transport identifié, une géométrie confirmée et des essais physiques documentés. Ne pas considérer une simple correspondance de marque comme une compatibilité.

Tester d’abord avec `--preview` : aucun transfert USB, aucune capture PresentMon. Tester ensuite le package autonome sur les appareils concernés. Ne jamais lancer plusieurs écrivains USB en même temps.

Pour un rapport de problème, préciser Windows, version de LOU, modèle des appareils et comportement attendu. Retirer noms de comptes, numéros de série, chemins privés et configurations personnelles des journaux avant de les partager.

Les packages compilés vont dans les Releases, pas dans l’historique Git. Fournir les sources correspondant exactement aux binaires et conserver les notices de licence. Le workflow fourni compile et archive les packages ; leur publication reste manuelle.
