# GED légère — Clean Architecture en C# (sans framework)

Gestion électronique de documents : dépôt de fichiers, versions, dossiers, tags, recherche, corbeille.

- **Aucun framework** : ni ASP.NET, ni Entity Framework, ni conteneur d'injection, ni framework de test.
  Uniquement la bibliothèque standard .NET (ADO.NET, `System.Text.Json`, `System.Security.Cryptography`).
- Les seules dépendances externes sont les **drivers** de base de données (impossibles à éviter) :
  `Microsoft.Data.Sqlite`, `MySqlConnector`, `Microsoft.Data.SqlClient`.
- **On change de base sans toucher au code** : il suffit de modifier `ged.config.json`.

## Architecture

```
            ┌──────────────────────────────────────────────┐
            │  Ged.Cli  (présentation + composition)       │
            │   ┌──────────────────────────────────────┐   │
            │   │  Ged.Infrastructure  (adaptateurs)   │   │
            │   │   ┌──────────────────────────────┐   │   │
            │   │   │  Ged.Application (use cases) │   │   │
            │   │   │   ┌──────────────────────┐   │   │   │
            │   │   │   │  Ged.Domain (noyau)  │   │   │   │
            │   │   │   └──────────────────────┘   │   │   │
            │   │   └──────────────────────────────┘   │   │
            │   └──────────────────────────────────────┘   │
            └──────────────────────────────────────────────┘
             Les dépendances pointent toujours vers l'intérieur.
```

| Projet | Rôle | Dépend de |
|---|---|---|
| `Ged.Domain` | **Le noyau** : entités (`Document`, `DocumentVersion`, `Folder`), objets-valeurs (`Tag`, `MimeType`, `Checksum`…), règles métier | rien |
| `Ged.Application` | Cas d'utilisation (`UploadDocument`, `AddVersion`, `PurgeTrash`…) et **ports** (interfaces `IDocumentRepository`, `IFileStorage`, `IClock`…) | Domain |
| `Ged.Infrastructure` | **Adaptateurs** : dépôts SQL, dépôts en mémoire, stockage disque, SHA-256, horloge | Application |
| `Ged.Cli` | Ligne de commande + `CompositionRoot` (injection de dépendances faite à la main) + lecture de la configuration | Infrastructure |
| `Ged.Tests` | Tests du domaine, des cas d'utilisation, et tests de contrat des dépôts sur chaque base | Infrastructure |

## Règles métier

- Un document a au moins une version ; déposer un nouveau fichier crée une nouvelle version (rien n'est écrasé).
- Types autorisés : pdf, png, jpg/jpeg, txt, docx, xlsx. Taille maximale configurable (20 Mo par défaut).
- Une nouvelle version identique (même empreinte SHA-256) à la version courante est refusée.
- Deux documents actifs d'un même dossier ne peuvent pas avoir le même titre (insensible à la casse).
- Tags normalisés : `" Facture "` = `"facture"`.
- Un document supprimé va dans la corbeille ; il est purgé définitivement après 30 jours (configurable).
- Un document dans la corbeille ne peut être ni modifié ni téléchargé.
- Un dossier non vide (sous-dossiers ou documents) ne peut pas être supprimé.
- Si l'enregistrement en base échoue, le fichier déjà écrit est supprimé (pas de fichier orphelin).

## Changer de base de données

Dans `ged.config.json` :

```json
"database": {
  "provider": "mysql",
  "connectionString": "Server=localhost;Port=3306;Database=ged;User ID=ged;Password=ged"
}
```

| `provider` | Exemple de `connectionString` |
|---|---|
| `sqlite` | `Data Source=ged.db` |
| `mysql` / `mariadb` | `Server=localhost;Port=3306;Database=ged;User ID=ged;Password=ged` |
| `sqlserver` | `Server=localhost,1433;Database=ged;User ID=sa;Password=...;TrustServerCertificate=True` |

Des fichiers prêts à l'emploi sont dans `config/`. On peut aussi choisir le fichier avec une variable d'environnement :
`GED_CONFIG=config/ged.mysql.json ged list`.

Les tables sont créées automatiquement au premier lancement.

### Pourquoi ça marche sans changer le code

1. Le noyau et les cas d'utilisation ne connaissent que l'interface `IDocumentRepository`.
2. `SqlDocumentRepository` n'utilise que les classes abstraites d'ADO.NET (`DbConnection`, `DbCommand`)
   et du **SQL standard** compris par toutes les bases (paramètres `@nom`, pas de `LIMIT`/`TOP`, pas d'auto-incrément,
   pas d'`UPSERT` spécifique, identifiants `Guid` générés par l'application, dates en texte ISO 8601).
3. `SqlConnectionFactory` choisit le driver d'après le nom lu dans la configuration.

### Ajouter une nouvelle base (ex. PostgreSQL)

Ajouter le driver (`dotnet add src/Ged.Infrastructure package Npgsql`) et **une ligne** dans
`SqlConnectionFactory` : `["postgresql"] = NpgsqlFactory.Instance`. Le domaine, les cas d'utilisation et
les dépôts ne changent pas. Lancer ensuite les tests de contrat pour vérifier.

## Lancer le projet

Prérequis : SDK .NET 8.

```bash
dotnet build
dotnet run --project src/Ged.Cli -- help
```

Exemples :

```bash
ged folder create Factures
ged upload facture.pdf --title "EDF octobre" --folder <id-dossier> --tag facture --tag énergie
ged version <id> facture-corrigee.pdf
ged search edf --tag facture --type pdf
ged download <id> copie.pdf --version 1
ged show <id>
ged trash <id>     ged restore <id>     ged purge
```

## Tests

```bash
dotnet run --project tests/Ged.Tests
```

Par défaut : domaine, cas d'utilisation, dépôts en mémoire et SQLite.
Pour tester aussi MySQL et SQL Server (avec `docker compose up -d`) :

```bash
GED_TEST_MYSQL="Server=127.0.0.1;Port=3306;Database=ged;User ID=ged;Password=ged" \
GED_TEST_SQLSERVER="Server=127.0.0.1,1433;Database=ged;User ID=sa;Password=Ged_Passw0rd!;TrustServerCertificate=True" \
dotnet run --project tests/Ged.Tests
```

Les **mêmes tests de contrat** sont exécutés sur chaque base : s'ils passent partout, la base est interchangeable.

## Évolutions possibles

| Évolution | Ce qui change |
|---|---|
| API HTTP (`HttpListener`, sans framework) | Nouveau projet de présentation, le noyau ne bouge pas |
| PostgreSQL | Une ligne dans `SqlConnectionFactory` + le driver |
| Stockage S3 / MinIO | Nouvel adaptateur `IFileStorage` |
| Recherche plein texte / OCR | Nouveau port `ITextExtractor` + adaptateur |
| Utilisateurs et droits | Nouveau module dans le domaine |
