# Contrats et plugins .NET

Référencer Omnilingua.Dotnet et Omnilingua.Dotnet.SourceGen via NuGet, dans la version du Omnilingua utilisé. Le générateur
fournit PluginExport et PluginImport. Les annotations portent sur la classe
partial du plugin ; les interfaces restent de simples contrats locaux.

```csharp
using Omnilingua.Dotnet;

public interface ILogger {
    void info(string context, string message);
}

[PluginExport(typeof(ILogger))]
public sealed partial class Plugin : IPlugin {
    private readonly Logger _logger = new(); // Logger implémente ILogger.
    public object[] GetExport() => [_logger];
}
```

GetMetadata est produit automatiquement : contrats exportés, signatures, catalogue
des types et besoins importés. Ne pas l'écrire manuellement sur un plugin annoté.
La classe doit être partial, concrète, non générique et non imbriquée. Le chargeur
requiert un constructeur public sans paramètre.

PluginExport(typeof(IContrat), Name = "NomPublic", Version = 2) personnalise nom
et version ; par défaut ce sont le nom simple de l'interface et la version 1.
Plusieurs annotations permettent plusieurs contrats ; un objet peut implémenter
plusieurs contrats. GetExport() doit fournir exactement une implémentation par
contrat annoncé. Les objets nuls, non annoncés, manquants ou ambigus sont refusés.

Le Omnilingua appelle Init puis récupère les objets exportés et construit leurs callbacks
C ABI. Le plugin ne gère aucun pointeur ni registre d'exports. Le tableau retourné
par IPlugin.GetExport() contient des objets ; les adresses opaques restent une
responsabilité du Omnilingua et de l'ABI entre langages.

## Importer un service

```csharp
[PluginImport(typeof(ILogger))]
public sealed partial class Plugin : IPlugin {
    private ILogger? _logger;
    public void Init(PluginContext context) => _logger = context.GetRequiredService<ILogger>();
    public Task StartAsync(CancellationToken cancellationToken) {
        _logger!.info("MonPlugin", "Bonjour !");
        return Task.CompletedTask;
    }
}
```

PluginImport propose Name et Version comme PluginExport, ainsi que Optional = true.
Les objets sont dans context.Services, un IServiceProvider, avant Init. Utiliser
`context.GetRequiredService<T>()` pour un besoin obligatoire ou Services.GetService
pour une dépendance optionnelle avec repli local. La configuration JSON reste dans
context.Configuration. Le fournisseur est choisi dans dependencies du fichier JSON.

IPlugin fournit des implémentations vides pour Init, GetExport, StartAsync et
StopAsync : omettre les méthodes inutiles. Le consommateur ci-dessus n'a donc
aucune méthode d'export ou d'arrêt. Les tâches réelles restent à retourner si le
plugin effectue un travail asynchrone. Le Omnilingua arrête les consommateurs avant
leurs fournisseurs.

## Types, erreurs et compatibilité

Les méthodes publiques d'instance de l'interface sont décrites, y compris celles
héritées. Les propriétés ne sont pas exportées. Scalaires : void, bool, int, uint,
ulong, string ; enums int/uint/ulong. Modèles : classes/records à constructeur public
sans paramètre et propriétés publiques get/set ou get/init. Modèles imbriqués,
champs nullables, tableaux, `List<T>`, `IReadOnlyList<T>` et paramètres out sont acceptés.
Les collections générées sont bornées à 256 éléments.

HSG001 refuse les types non représentables, cycles, collections imbriquées, scalaires
`Nullable<T>` directement en paramètres/résultats, surcharges, méthodes statiques,
async ou génériques et paramètres ref/in. Les imports et exports dupliqués ou les
classes non partial produisent aussi un diagnostic.

Les copies sont profondes. La disposition générée utilise alignement naturel,
champs ordonnés par nom et bits de présence ; elle ne remplace pas la disposition
du fournisseur lors de l'injection. Une exception d'export est journalisée et
produit des sorties nulles/zéro ; définir un statut pour les erreurs métier.

Les plugins utilisent PluginImport/PluginExport. Les interfaces ne portent aucun
attribut et ne nécessitent pas de déclaration partial. Les proxies et conversions
génériques restent dynamiques ; le générateur ne garantit pas la compatibilité AOT.
