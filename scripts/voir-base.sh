#!/usr/bin/env bash
# Affiche le contenu de la base de la GED, quelle que soit la base configurée (SQLite, MySQL, SQL Server).
#
#   scripts/voir-base.sh                 résumé + toutes les tables
#   scripts/voir-base.sh documents       documents (avec leur dossier)
#   scripts/voir-base.sh versions        historique des versions
#   scripts/voir-base.sh tags            tags et nombre de documents
#   scripts/voir-base.sh dossiers        dossiers
#   scripts/voir-base.sh corbeille       documents dans la corbeille
#   scripts/voir-base.sh stats           compteurs et taille totale
#   scripts/voir-base.sh fichiers        fichiers présents dans le dossier de stockage
#   scripts/voir-base.sh sql "SELECT …"  requête libre
#   scripts/voir-base.sh shell           console SQL interactive
#
# La base est lue dans ged.config.json (ou le fichier indiqué par GED_CONFIG).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${GED_CONFIG:-$ROOT/ged.config.json}"
[[ -f "$CONFIG" ]] || { echo "Configuration introuvable : $CONFIG" >&2; exit 1; }

# Lecture de la configuration (le JSON peut contenir des commentaires //).
read_config() {
  python3 - "$CONFIG" "$1" <<'PY'
import json, re, sys
text = re.sub(r'^\s*//.*$', '', open(sys.argv[1], encoding='utf-8').read(), flags=re.M)
cfg = json.loads(text)
db = cfg.get('database', {})
key = sys.argv[2]
if key == 'provider':
    print(db.get('provider', 'sqlite').lower())
elif key == 'storage':
    print(cfg.get('storagePath', 'storage'))
else:  # clé de la chaîne de connexion
    parts = dict(p.split('=', 1) for p in db.get('connectionString', '').split(';') if '=' in p)
    parts = {k.strip().lower(): v.strip() for k, v in parts.items()}
    aliases = {'server': ['server', 'host', 'data source'], 'port': ['port'],
               'database': ['database', 'initial catalog'], 'user': ['user id', 'uid', 'user', 'username'],
               'password': ['password', 'pwd'], 'file': ['data source']}
    print(next((parts[a] for a in aliases[key] if a in parts), ''))
PY
}

PROVIDER="$(read_config provider)"
cd "$ROOT"

# Si le client « sqlite3 » n'est pas installé, Python (présent sur Ubuntu) affiche le résultat.
sqlite_with_python() {
  python3 - "$1" "$2" <<'PY'
import sqlite3, sys
con = sqlite3.connect(sys.argv[1])
cur = con.execute(sys.argv[2])
if cur.description is None:
    con.commit(); print(f"{cur.rowcount} ligne(s) modifiée(s)."); sys.exit()
head = [d[0] for d in cur.description]
rows = [["" if v is None else str(v) for v in r] for r in cur.fetchall()]
w = [max(len(x) for x in col) for col in zip(head, *rows)]
line = "+" + "+".join("-" * (n + 2) for n in w) + "+"
fmt = lambda r: "| " + " | ".join(v.ljust(n) for v, n in zip(r, w)) + " |"
print(line); print(fmt(head)); print(line)
for r in rows: print(fmt(r))
print(line); print(f"{len(rows)} ligne(s)")
PY
}

run_sql() {
  local sql="$1"
  case "$PROVIDER" in
    sqlite)
      local file; file="$(read_config file)"
      [[ -f "$file" ]] || { echo "Base SQLite introuvable : $file (lancez d'abord la GED une fois)" >&2; exit 1; }
      if command -v sqlite3 >/dev/null; then
        sqlite3 -box "$file" "$sql"
      else
        sqlite_with_python "$file" "$sql"
      fi
      ;;
    mysql|mariadb)
      local user pass db host port
      user="$(read_config user)"; pass="$(read_config password)"; db="$(read_config database)"
      host="$(read_config server)"; port="$(read_config port)"
      if command -v mysql >/dev/null; then
        MYSQL_PWD="$pass" mysql -h "${host:-127.0.0.1}" -P "${port:-3306}" -u "$user" "$db" --table -e "$sql"
      else
        docker compose exec -T -e MYSQL_PWD="$pass" mysql mysql -u "$user" "$db" --table -e "$sql"
      fi
      ;;
    sqlserver)
      local user pass db
      user="$(read_config user)"; pass="$(read_config password)"; db="$(read_config database)"
      if command -v sqlcmd >/dev/null; then
        sqlcmd -S "$(read_config server)" -U "$user" -P "$pass" -d "$db" -C -W -s "|" -Q "SET NOCOUNT ON; $sql"
      else
        docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$user" -P "$pass" -d "$db" \
          -C -W -s "|" -Q "SET NOCOUNT ON; $sql"
      fi
      ;;
    *) echo "Base non gérée par ce script : $PROVIDER" >&2; exit 1 ;;
  esac
}

open_shell() {
  case "$PROVIDER" in
    sqlite)
      command -v sqlite3 >/dev/null || { echo "Console interactive : installez sqlite3 (sudo apt install sqlite3)" >&2; exit 1; }
      sqlite3 -box "$(read_config file)" ;;
    mysql|mariadb)
      if command -v mysql >/dev/null; then
        MYSQL_PWD="$(read_config password)" mysql -h "$(read_config server)" -P "$(read_config port)" \
          -u "$(read_config user)" "$(read_config database)"
      else
        docker compose exec -e MYSQL_PWD="$(read_config password)" mysql mysql -u "$(read_config user)" "$(read_config database)"
      fi ;;
    sqlserver)
      docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$(read_config user)" \
        -P "$(read_config password)" -d "$(read_config database)" -C ;;
  esac
}

# Requêtes en SQL standard : elles fonctionnent sur toutes les bases.
Q_DOCUMENTS="SELECT d.id, d.title AS titre, d.status AS statut, COALESCE(f.name, '(racine)') AS dossier,
  (SELECT MAX(v.version_number) FROM document_versions v WHERE v.document_id = d.id) AS version, d.created_at AS cree_le
FROM documents d LEFT JOIN folders f ON f.id = d.folder_id ORDER BY d.title_normalized"

Q_VERSIONS="SELECT d.title AS document, v.version_number AS version, v.original_file_name AS fichier,
  v.mime_type AS type, v.size_bytes AS octets, v.storage_key AS cle_stockage, v.created_at AS cree_le
FROM document_versions v JOIN documents d ON d.id = v.document_id ORDER BY d.title_normalized, v.version_number"

Q_TAGS="SELECT t.tag, COUNT(*) AS nb_documents FROM document_tags t GROUP BY t.tag ORDER BY t.tag"

Q_DOSSIERS="SELECT f.id, f.name AS nom, COALESCE(p.name, '-') AS parent,
  (SELECT COUNT(*) FROM documents d WHERE d.folder_id = f.id) AS nb_documents
FROM folders f LEFT JOIN folders p ON p.id = f.parent_id ORDER BY f.name"

Q_CORBEILLE="SELECT id, title AS titre, trashed_at AS mis_en_corbeille_le FROM documents
WHERE status = 'Trashed' ORDER BY trashed_at"

Q_STATS="SELECT
  (SELECT COUNT(*) FROM documents WHERE status = 'Active') AS documents_actifs,
  (SELECT COUNT(*) FROM documents WHERE status = 'Trashed') AS dans_corbeille,
  (SELECT COUNT(*) FROM document_versions) AS versions,
  (SELECT COUNT(*) FROM folders) AS dossiers,
  (SELECT COUNT(DISTINCT tag) FROM document_tags) AS tags,
  (SELECT COALESCE(SUM(size_bytes), 0) FROM document_versions) AS octets_stockes"

title() { printf '\n\033[1m== %s\033[0m\n' "$1"; }

case "${1:-tout}" in
  documents) run_sql "$Q_DOCUMENTS" ;;
  versions)  run_sql "$Q_VERSIONS" ;;
  tags)      run_sql "$Q_TAGS" ;;
  dossiers)  run_sql "$Q_DOSSIERS" ;;
  corbeille) run_sql "$Q_CORBEILLE" ;;
  stats)     run_sql "$Q_STATS" ;;
  sql)       run_sql "${2:?Usage : $0 sql \"SELECT ...\"}" ;;
  shell)     open_shell ;;
  fichiers)
    STORAGE="$(read_config storage)"
    [[ -d "$STORAGE" ]] || { echo "Dossier de stockage vide ou absent : $STORAGE"; exit 0; }
    echo "Dossier : $STORAGE — $(find "$STORAGE" -type f | wc -l) fichier(s), $(du -sh "$STORAGE" | cut -f1)"
    find "$STORAGE" -type f -printf '%TY-%Tm-%Td %TH:%TM  %8s o  %f\n' | sort
    ;;
  tout)
    echo "Base : $PROVIDER  (configuration : $CONFIG)"
    title "Statistiques";  run_sql "$Q_STATS"
    title "Dossiers";      run_sql "$Q_DOSSIERS"
    title "Documents";     run_sql "$Q_DOCUMENTS"
    title "Versions";      run_sql "$Q_VERSIONS"
    title "Tags";          run_sql "$Q_TAGS"
    ;;
  -h|--help|help) sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//' ;;
  *) echo "Commande inconnue : $1 (voir $0 help)" >&2; exit 1 ;;
esac
