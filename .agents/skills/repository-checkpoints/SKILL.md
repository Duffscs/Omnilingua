---
name: repository-checkpoints
description: Créer des commits locaux cohérents et nettoyer les dossiers vides du projet courant.
---

Examiner status et diff, préserver les modifications utilisateur et paramètres
locaux. Créer des commits sémantiques aux étapes réellement validées ; indiquer
les vérifications et limites. Ne pas pousser, amender ou réécrire l'historique.
Indexer uniquement les chemins de la tâche, jamais tout le dépôt aveuglément.

scripts/Remove-EmptyDirectories.ps1 accepte -Root et -WhatIf. Il ne supprime
que les dossiers effectivement vides, sans récursion de suppression ni jonctions,
et préserve .git, .vs, .vscode et .tmp. Ne pas supprimer des artefacts pour rendre
un dossier vide ou arrêter un processus utilisateur pour débloquer un fichier.

Limiter les opérations au projet courant. Si le dépôt Git contient d'autres
arbres, ne pas les indexer ou les parcourir pour réaliser une tâche locale.
