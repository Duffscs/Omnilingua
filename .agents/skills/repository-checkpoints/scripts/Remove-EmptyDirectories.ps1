[CmdletBinding(SupportsShouldProcess)]
param(
    [string] $Root = (Join-Path $PSScriptRoot '../../../..')
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = (Resolve-Path -LiteralPath $Root).Path.TrimEnd([char[]] '\/')
if (-not (Test-Path -LiteralPath (Join-Path $workspaceRoot 'AGENTS.md'))) {
    throw 'La racine doit être un dépôt contenant AGENTS.md.'
}
$protectedNames = @('.git', '.vs', '.vscode', '.tmp')

function Remove-EmptyChildren {
    param([string] $ParentPath)

    foreach ($directory in @(Get-ChildItem -LiteralPath $ParentPath -Directory -Force)) {
        if ($protectedNames -contains $directory.Name -or
            ($directory.Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
            continue
        }
        $directoryPath = [System.IO.Path]::GetFullPath($directory.FullName)
        if (-not $directoryPath.StartsWith($workspaceRoot + [System.IO.Path]::DirectorySeparatorChar,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Répertoire hors de la racine : $directoryPath"
        }
        Remove-EmptyChildren -ParentPath $directoryPath
        if (@(Get-ChildItem -LiteralPath $directoryPath -Force).Count -eq 0 -and
            $PSCmdlet.ShouldProcess($directoryPath, 'Supprimer le répertoire vide')) {
            try {
                # Sans Recurse : un fichier créé entre le contrôle et la suppression est préservé.
                [System.IO.Directory]::Delete($directoryPath, $false)
                Write-Output $directoryPath
            } catch [System.IO.IOException] {
                Write-Warning "Répertoire conservé, verrouillé ou redevenu non vide : $directoryPath"
            } catch [System.UnauthorizedAccessException] {
                Write-Warning "Répertoire conservé, accès refusé : $directoryPath"
            }
        }
    }
}

Remove-EmptyChildren -ParentPath $workspaceRoot
