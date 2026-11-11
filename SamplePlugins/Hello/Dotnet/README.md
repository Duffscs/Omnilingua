# Hello.Dotnet

Consommateur .NET minimal : son interface locale ILogger ne déclare que info. PluginImport sur la classe
partial génère GetMetadata pour cette dépendance obligatoire, Init résout
le service et StartAsync journalise « Bonjour depuis le plugin .NET ! ».
Le projet référence uniquement les packages SDK, sans dépendre du projet Logger.

Depuis la racine de la plateforme :

```powershell
./build.ps1 -Configuration Release
./Omnilingua/Runtime/bin/Release/net10.0/Omnilingua.exe ./Omnilingua/Runtime/bin/Release/net10.0/plugins.hello.dotnet.json
```

Résultat attendu avec le logger C# :

```text
[LoggerDotnet] ... [INFO] [Hello.Dotnet] Bonjour depuis le plugin .NET !
```

Le fournisseur ILogger peut être remplacé par le logger C++, Java ou Python dans
la configuration sans modifier ce plugin. Les tests Logger.SmokeTests vérifient
ces quatre combinaisons. Arrêter le Omnilingua avec Ctrl+C.
