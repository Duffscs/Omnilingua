# Hello.Java

Ce plugin déclare `@PluginImport(ILogger.class)` sur sa classe d'entrée.
L'interface locale ILogger est un contrat simple, sans annotation.
Le SDK décrit automatiquement l'import, puis le Omnilingua fournit son implémentation
dans `context.services().getRequiredService(ILogger.class)` avant le démarrage.

Depuis la racine de la plateforme :

```powershell
./build.ps1 -Configuration Release
./Omnilingua/Runtime/bin/Release/net10.0/Omnilingua.exe ./Omnilingua/Runtime/bin/Release/net10.0/plugins.hello.java.json
```

Le message est « Bonjour depuis le plugin Java ! », avec Hello.Java comme contexte.
La configuration utilise le logger .NET ; les fournisseurs Java, C++ et Python
sont interchangeables sans modifier le plugin. Logger.SmokeTests vérifie les quatre.
