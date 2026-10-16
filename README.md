# GED légère — Clean Architecture en C# (sans framework)

[![CI](https://github.com/FaniriniainaR/ged-clean-architecture/actions/workflows/ci.yml/badge.svg)](https://github.com/FaniriniainaR/ged-clean-architecture/actions/workflows/ci.yml)

Gestion électronique de documents : dépôt de fichiers, versions, dossiers, tags, recherche, corbeille.

- **Aucun framework** : ni ASP.NET, ni Entity Framework, ni conteneur d'injection, ni framework de test.
  Uniquement la bibliothèque standard .NET (ADO.NET, `System.Text.Json`, `System.Security.Cryptography`).
- Les seules dépendances externes sont les **drivers** de base de données (impossibles à éviter) :
  `Microsoft.Data.Sqlite`, `MySqlConnector`, `Microsoft.Data.SqlClient`.
- **On change de base sans toucher au code** : il suffit de modifier `ged.config.json`.
- **On peut utiliser deux bases différentes en même temps** : par exemple les documents dans MySQL et les dossiers dans SQL Server.
- **Deux interfaces sur le même noyau** : une interface web et une ligne de commande.

## Architecture

```
            ┌──────────────────────────────────────────────┐
            │  Ged.Web / Ged.Cli  (présentation)           │
            │  Ged.Bootstrap      (composition)            │
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
| `Ged.Bootstrap` | Lecture de la configuration + `CompositionRoot` (injection de dépendances faite à la main), partagé par le web et la CLI | Infrastructure |
| `Ged.Web` | Interface web : serveur `HttpListener`, routage, lecture des formulaires (y compris l'envoi de fichiers), pages HTML | Bootstrap |
| `Ged.Cli` | Ligne de commande | Bootstrap |
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

## Utiliser deux bases différentes en même temps

Les documents (avec leurs versions et leurs tags) et les dossiers peuvent être dans **deux bases différentes**,
même de moteurs différents. Il suffit d'ajouter une section `databases` (exemple complet : `config/ged.deux-bases.json`) :

```json
"database": { "provider": "sqlite", "connectionString": "Data Source=ged.db" },
"databases": {
  "documents": { "provider": "mysql",     "connectionString": "Server=localhost;Port=3306;Database=ged;User ID=ged;Password=ged" },
  "folders":   { "provider": "sqlserver", "connectionString": "Server=localhost,1433;Database=ged;User ID=sa;Password=...;TrustServerCertificate=True" }
}
```

- `database` reste la base par défaut : une partie absente de `databases` va dans `database`.
- Sans section `databases`, tout est dans la même base, comme avant.
- Au démarrage, chaque base ne reçoit que ses propres tables : `documents`, `document_versions` et `document_tags`
  d'un côté, `folders` de l'autre.

| Partie | Tables | Port utilisé par le noyau |
|---|---|---|
| `documents` | `documents`, `document_versions`, `document_tags` | `IDocumentRepository` |
| `folders` | `folders` | `IFolderRepository` |

**Pourquoi ça marche sans changer le code :**

1. Les cas d'utilisation ne parlent qu'aux interfaces `IDocumentRepository` et `IFolderRepository`.
   Ils ne savent pas si elles utilisent la même base ou deux bases différentes.
2. Aucune requête SQL ne fait de jointure entre les documents et les dossiers, et il n'y a pas de clé étrangère entre eux.
   Les règles qui les relient sont dans les cas d'utilisation : « le dossier doit exister » (`UploadDocument`, `MoveDocument`)
   et « un dossier non vide ne peut pas être supprimé » (`DeleteFolder`).
3. Dans `CompositionRoot`, chaque dépôt reçoit sa propre connexion :

```csharp
var documentsDb = new SqlConnectionFactory(config.DocumentsDatabase);
var foldersDb = new SqlConnectionFactory(config.FoldersDatabase);
var documents = new SqlDocumentRepository(documentsDb);
var folders = new SqlFolderRepository(foldersDb);
```

**Limite :** il n'y a pas de transaction commune aux deux bases. Une opération qui modifie les deux à la fois
ne serait pas annulée entièrement en cas d'erreur. Aujourd'hui, aucun cas d'utilisation n'écrit dans les deux bases
à la fois : chacun écrit dans une seule base et se contente de lire l'autre.

Démonstration :

```bash
scripts/bases-docker.sh start
GED_CONFIG=config/ged.deux-bases.json dotnet run --project src/Ged.Web
GED_CONFIG=config/ged.deux-bases.json scripts/voir-base.sh
```

### Ajouter une nouvelle base (ex. PostgreSQL)

Ajouter le driver (`dotnet add src/Ged.Infrastructure package Npgsql`) et **une ligne** dans
`SqlConnectionFactory` : `["postgresql"] = NpgsqlFactory.Instance`. Le domaine, les cas d'utilisation et
les dépôts ne changent pas. Lancer ensuite les tests de contrat pour vérifier.

## Lancer le projet

Prérequis : SDK .NET 8 (`sudo apt install dotnet-sdk-8.0`). Toutes les commandes se lancent depuis la racine du projet.

```bash
dotnet build
```

### Interface web

```bash
dotnet run --project src/Ged.Web
```

Puis ouvrir **http://localhost:5000** dans le navigateur. Sous WSL, l'adresse fonctionne aussi depuis le navigateur Windows.

| Page | Contenu |
|---|---|
| `/` | Liste des documents, recherche (titre, tag, type), filtre par dossier, formulaire d'envoi |
| `/documents/{id}` | Détails, historique des versions, téléchargement, nouvelle version, tags, renommer, déplacer, corbeille |
| `/folders` | Liste, création et suppression des dossiers |
| `/trash` | Corbeille : restaurer, purger |

Le port se change dans `ged.config.json` (`"webUrl"`) ou au lancement :
`dotnet run --project src/Ged.Web -- http://localhost:8080/`.

Le serveur web est écrit avec `HttpListener` (bibliothèque standard), sans ASP.NET. Comme la CLI, il ne contient
aucune règle métier : il traduit les requêtes HTTP en appels aux cas d'utilisation. Les erreurs métier
(doublon, type refusé, dossier non vide…) s'affichent en message dans la page.

### Ligne de commande

```bash
dotnet run --project src/Ged.Cli -- help
```

Exemples (`ged` = `dotnet run --project src/Ged.Cli --`) :

```bash
ged folder create Factures
ged upload facture.pdf --title "EDF octobre" --folder <id-dossier> --tag facture --tag énergie
ged version <id> facture-corrigee.pdf
ged search edf --tag facture --type pdf
ged download <id> copie.pdf --version 1
ged show <id>
ged trash <id>     ged restore <id>     ged purge
```

## Regarder le contenu de la base

Le script `scripts/voir-base.sh` lit `ged.config.json` et interroge la bonne base (SQLite, MySQL ou SQL Server) :

```bash
scripts/voir-base.sh               # statistiques + dossiers + documents + versions + tags
scripts/voir-base.sh documents     # documents, avec leur dossier et leur version courante
scripts/voir-base.sh versions      # historique des versions (fichier, type, taille, clé de stockage)
scripts/voir-base.sh tags          # tags et nombre de documents
scripts/voir-base.sh dossiers      # dossiers, parent et nombre de documents
scripts/voir-base.sh corbeille     # documents dans la corbeille
scripts/voir-base.sh stats         # compteurs et taille totale
scripts/voir-base.sh fichiers      # fichiers présents dans le dossier de stockage
scripts/voir-base.sh sql "SELECT * FROM documents"        # requête libre (base des documents)
scripts/voir-base.sh sql-dossiers "SELECT * FROM folders"  # requête libre (base des dossiers)
scripts/voir-base.sh shell         # console SQL interactive (shell dossiers : base des dossiers)
```

Pour regarder une autre base : `GED_CONFIG=config/ged.mysql.json scripts/voir-base.sh`.

Avec deux bases séparées, le script interroge chacune pour ses propres tables. Comme on ne peut pas faire de jointure
entre deux bases, la liste des documents affiche l'identifiant du dossier (`id_dossier`) au lieu de son nom.

| Base | Outil utilisé |
|---|---|
| SQLite | `sqlite3` s'il est installé (`sudo apt install sqlite3`), sinon Python (déjà présent sur Ubuntu) |
| MySQL | client `mysql` s'il est installé, sinon le conteneur Docker |
| SQL Server | `sqlcmd` s'il est installé, sinon le conteneur Docker |

### Structure de la base

| Table | Contenu |
|---|---|
| `folders` | `id`, `name`, `parent_id`, `created_at` |
| `documents` | `id`, `title`, `title_normalized` (minuscules, pour la recherche), `folder_id`, `status` (`Active` / `Trashed`), `created_at`, `trashed_at` |
| `document_versions` | `document_id`, `version_number`, `storage_key` (nom du fichier dans `storage/`), `original_file_name`, `mime_type`, `size_bytes`, `checksum` (SHA-256), `created_at` |
| `document_tags` | `document_id`, `tag` |

Les fichiers eux-mêmes ne sont pas dans la base : ils sont dans le dossier `storage/`, rangés par clé de stockage.

## Bases MySQL et SQL Server avec Docker

```bash
scripts/bases-docker.sh start    # démarre MySQL (3306) et SQL Server (1433) et crée la base « ged »
scripts/bases-docker.sh status
scripts/bases-docker.sh stop     # arrête et supprime les conteneurs (données perdues)
```

Puis, par exemple, lancer le site sur MySQL sans changer le code :

```bash
GED_CONFIG=config/ged.mysql.json dotnet run --project src/Ged.Web
GED_CONFIG=config/ged.mysql.json scripts/voir-base.sh
```

## Tests

```bash
dotnet run --project tests/Ged.Tests
```

Par défaut : domaine, cas d'utilisation, dépôts en mémoire et SQLite.
Pour tester aussi MySQL et SQL Server (après `scripts/bases-docker.sh start`) :

```bash
GED_TEST_MYSQL="Server=127.0.0.1;Port=3306;Database=ged;User ID=ged;Password=ged" \
GED_TEST_SQLSERVER="Server=127.0.0.1,1433;Database=ged;User ID=sa;Password=Ged_Passw0rd!;TrustServerCertificate=True" \
dotnet run --project tests/Ged.Tests
```

Les **mêmes tests de contrat** sont exécutés sur chaque base : s'ils passent partout, la base est interchangeable.

Les tests « Deux bases » lancent les vrais cas d'utilisation avec les documents dans une base et les dossiers dans une autre :
deux fichiers SQLite par défaut, et MySQL + SQL Server quand les deux variables ci-dessus sont définies.

### Intégration continue

À chaque push sur `main` et à chaque pull request, GitHub Actions (`.github/workflows/ci.yml`) compile le projet
et lance les tests : d'abord sur SQLite, puis sur MySQL et SQL Server (démarrés comme conteneurs par GitHub).
Les mêmes tests de contrat passent donc sur les trois bases à chaque modification.

## Évolutions possibles

| Évolution | Ce qui change |
|---|---|
| API JSON (pour une application mobile) | Nouveau projet de présentation, le noyau ne bouge pas |
| PostgreSQL | Une ligne dans `SqlConnectionFactory` + le driver |
| Stockage S3 / MinIO | Nouvel adaptateur `IFileStorage` |
| Recherche plein texte / OCR | Nouveau port `ITextExtractor` + adaptateur |
| Utilisateurs et droits | Nouveau module dans le domaine |
