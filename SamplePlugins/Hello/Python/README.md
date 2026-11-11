# Hello.Python

Consommateur minimal de ILogger, sans reference au code du fournisseur.
PluginImport annonce le besoin et le Omnilingua injecte une implementation locale.

Depuis la racine de Omnilingua :

```powershell
./build.ps1 -Configuration Release
& ./Omnilingua/Runtime/bin/Release/net10.0/Omnilingua.exe ./Omnilingua/Runtime/bin/Release/net10.0/plugins.hello.python.json
```

Le message est emis au demarrage. Remplacer uniquement le fournisseur Logger
dans la configuration pour tester les versions .NET, C++, Java ou Python.
