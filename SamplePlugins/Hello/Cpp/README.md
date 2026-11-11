# Hello.Cpp

Consommateur C++ du contrat ILogger, sans référence à une implémentation de logger.
La classe Plugin porte `OMNILINGUA_PLUGIN_IMPORT(ILogger)`, puis obtient une interface
locale avec `context.Services.GetRequiredService<ILogger>()` dans Init.
Le générateur analyse Plugin.h via CppPlugin. Plugin.cpp inclut Plugin.g.h puis
déclare OMNILINGUA_PLUGIN(HelloCpp::Plugin). Le SDK prend en charge les points d'entrée.

Depuis la racine de Omnilingua :

```powershell
./build.ps1 -Configuration Release
& ./Omnilingua/Runtime/bin/Release/net10.0/Omnilingua.exe ./Omnilingua/Runtime/bin/Release/net10.0/plugins.hello.cpp.json
```

Remplacer uniquement le fournisseur Logger dans ce fichier pour essayer les
implémentations C++, .NET, Java et Python. Le message est émis dans Start.
