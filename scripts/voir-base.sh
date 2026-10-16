#!/usr/bin/env bash
# Affiche le contenu de la base de la GED, quelle que soit la base configurée (SQLite, MySQL, SQL Server).
# Fonctionne aussi quand documents et dossiers sont dans deux bases différentes (section « databases »).
#
#   scripts/voir-base.sh                 résumé + toutes les tables
#   scripts/voir-base.sh documents       documents (avec leur dossier)
#   scripts/voir-base.sh versions        historique des versions
#   scripts/voir-base.sh tags            tags et nombre de documents
#   scripts/voir-base.sh dossiers        dossiers
#   scripts/voir-base.sh corbeille       documents dans la corbeille
#   scripts/voir-base.sh stats           compteurs et taille totale
#   scripts/voir-base.sh fichiers        fichiers présents dans le dossier de stockage
#   scripts/voir-base.sh sql "SELECT …"  requête libre (base des documents)
#   scripts/voir-base.sh sql-dossiers "…" requête libre (base des dossiers)
#   scripts/voir-base.sh shell [dossiers] console SQL interactive
#
# La base est lue dans ged.config.json (ou le fichier indiqué par GED_CONFIG).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${GED_CONFIG:-$ROOT/ged.config.json}"
[[ -f "$CONFIG" ]] || { echo "Configuration introuvable : $CONFIG" >&2; exit 1; }

# Lecture de la configuration (le JSON peut contenir des commentaires //).
#   read_config <documents|folders> <clé>
# La base d'une partie est « databases.<partie> » si elle existe, sinon « database ».
read_config() {
  python3 - "$CONFIG" "$1" "$2" <<'PY'
import json, re, sys
text = re.sub(r'^\s*//.*$', '', open(sys.argv[1], encoding='utf-8').read(), flags=re.M)
cfg = {k.lower(): v for k, v in json.loads(text).items()}
parts = {k.lower(): v for k, v in (cfg.get('databases') or {}).items()}
db = parts.get(sys.argv[2]) or cfg.get('database', {})
db = {k.lower(): v for k, v in db.items()}
key = sys.argv[3]
if key == 'signature':
    print(db.get('provider', '').lower() + '|' + db.get('connectionstring', ''))
elif key == 'provider':
    print(db.get('provider', 'sqlite').lower())
elif key == 'storage':
    print(cfg.get('storagepath', 'storage'))
else:  # clé de la chaîne de connexion
    parts = dict(p.split('=', 1) for p in db.get('connectionstring', '').split(';') if '=' in p)
    parts = {k.strip().lower(): v.strip() for k, v in parts.items()}
    aliases = {'server': ['server', 'host', 'data source'], 'port': ['port'],
               'database': ['database', 'initial catalog'], 'user': ['user id', 'uid', 'user', 'username'],
               'password': ['password', 'pwd'], 'file': ['data source']}
    print(next((parts[a] for a in aliases[key] if a in parts), ''))
PY
}

cd "$ROOT"

# Documents et dossiers dans la même base, ou dans deux bases différentes ?
if [[ "$(read_config documents signature)" == "$(read_config folders signature)" ]]; then SPLIT=0; else SPLIT=1; fi

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

# run_sql <documents|folders> <requête>
run_sql() {
  local part="$1" sql="$2"
  rc() { read_config "$part" "$1"; }
  case "$(rc provider)" in
    sqlite)
      local file; file="$(rc file)"
      [[ -f "$file" ]] || { echo "Base SQLite introuvable : $file (lancez d'abord la GED une fois)" >&2; exit 1; }
      if command -v sqlite3 >/dev/null; then
        sqlite3 -box "$file" "$sql"
      else
        sqlite_with_python "$file" "$sql"
      fi
      ;;
    mysql|mariadb)
      local user pass db host port
      user="$(rc user)"; pass="$(rc password)"; db="$(rc database)"
      host="$(rc server)"; port="$(rc port)"
      if command -v mysql >/dev/null; then
        MYSQL_PWD="$pass" mysql -h "${host:-127.0.0.1}" -P "${port:-3306}" -u "$user" "$db" --table -e "$sql"
      else
        docker compose exec -T -e MYSQL_PWD="$pass" mysql mysql -u "$user" "$db" --table -e "$sql"
      fi
      ;;
    sqlserver)
      local user pass db
      user="$(rc user)"; pass="$(rc password)"; db="$(rc database)"
      if command -v sqlcmd >/dev/null; then
        sqlcmd -S "$(rc server)" -U "$user" -P "$pass" -d "$db" -C -W -s "|" -Q "SET NOCOUNT ON; $sql"
      else
        docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$user" -P "$pass" -d "$db" \
          -C -W -s "|" -Q "SET NOCOUNT ON; $sql"
      fi
      ;;
    *) echo "Base non gérée par ce script : $(rc provider)" >&2; exit 1 ;;
  esac
}

# open_shell <documents|folders>
open_shell() {
  local part="$1"
  rc() { read_config "$part" "$1"; }
  case "$(rc provider)" in
    sqlite)
      command -v sqlite3 >/dev/null || { echo "Console interactive : installez sqlite3 (sudo apt install sqlite3)" >&2; exit 1; }
      sqlite3 -box "$(rc file)" ;;
    mysql|mariadb)
      if command -v mysql >/dev/null; then
        MYSQL_PWD="$(rc password)" mysql -h "$(rc server)" -P "$(rc port)" -u "$(rc user)" "$(rc database)"
      else
        docker compose exec -e MYSQL_PWD="$(rc password)" mysql mysql -u "$(rc user)" "$(rc database)"
      fi ;;
    sqlserver)
      docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U "$(rc user)" \
        -P "$(rc password)" -d "$(rc database)" -C ;;
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

# Deux bases séparées : pas de jointure possible entre documents et dossiers, chaque requête reste dans sa base.
if [[ $SPLIT == 1 ]]; then
  Q_DOCUMENTS="SELECT d.id, d.title AS titre, d.status AS statut, COALESCE(d.folder_id, '(racine)') AS id_dossier,
  (SELECT MAX(v.version_number) FROM document_versions v WHERE v.document_id = d.id) AS version, d.created_at AS cree_le
FROM documents d ORDER BY d.title_normalized"

  Q_DOSSIERS="SELECT f.id, f.name AS nom, COALESCE(p.name, '-') AS parent
FROM folders f LEFT JOIN folders p ON p.id = f.parent_id ORDER BY f.name"

  Q_STATS="SELECT
  (SELECT COUNT(*) FROM documents WHERE status = 'Active') AS documents_actifs,
  (SELECT COUNT(*) FROM documents WHERE status = 'Trashed') AS dans_corbeille,
  (SELECT COUNT(*) FROM document_versions) AS versions,
  (SELECT COUNT(DISTINCT tag) FROM document_tags) AS tags,
  (SELECT COALESCE(SUM(size_bytes), 0) FROM document_versions) AS octets_stockes"
  Q_STATS_DOSSIERS="SELECT COUNT(*) AS dossiers FROM folders"
fi

title() { printf '\n\033[1m== %s\033[0m\n' "$1"; }

stats() {
  run_sql documents "$Q_STATS"
  if [[ $SPLIT == 1 ]]; then run_sql folders "$Q_STATS_DOSSIERS"; fi
}

case "${1:-tout}" in
  documents) run_sql documents "$Q_DOCUMENTS" ;;
  versions)  run_sql documents "$Q_VERSIONS" ;;
  tags)      run_sql documents "$Q_TAGS" ;;
  dossiers)  run_sql folders "$Q_DOSSIERS" ;;
  corbeille) run_sql documents "$Q_CORBEILLE" ;;
  stats)     stats ;;
  sql)          run_sql documents "${2:?Usage : $0 sql \"SELECT ...\"}" ;;
  sql-dossiers) run_sql folders "${2:?Usage : $0 sql-dossiers \"SELECT ...\"}" ;;
  shell)     if [[ "${2:-}" == dossiers ]]; then open_shell folders; else open_shell documents; fi ;;
  fichiers)
    STORAGE="$(read_config documents storage)"
    [[ -d "$STORAGE" ]] || { echo "Dossier de stockage vide ou absent : $STORAGE"; exit 0; }
    echo "Dossier : $STORAGE — $(find "$STORAGE" -type f | wc -l) fichier(s), $(du -sh "$STORAGE" | cut -f1)"
    # Python plutôt que « find -printf », absent de macOS.
    python3 - "$STORAGE" <<'PY' | sort
import os, sys, time
for d, _, files in os.walk(sys.argv[1]):
    for f in files:
        st = os.stat(os.path.join(d, f))
        print(f"{time.strftime('%Y-%m-%d %H:%M', time.localtime(st.st_mtime))}  {st.st_size:8d} o  {f}")
PY
    ;;
  tout)
    if [[ $SPLIT == 1 ]]; then
      echo "Deux bases : documents → $(read_config documents provider), dossiers → $(read_config folders provider)"
    else
      echo "Base : $(read_config documents provider)"
    fi
    echo "(configuration : $CONFIG)"
    title "Statistiques";  stats
    title "Dossiers$([[ $SPLIT == 1 ]] && echo " — base $(read_config folders provider)")";      run_sql folders "$Q_DOSSIERS"
    title "Documents$([[ $SPLIT == 1 ]] && echo " — base $(read_config documents provider)")";   run_sql documents "$Q_DOCUMENTS"
    title "Versions";      run_sql documents "$Q_VERSIONS"
    title "Tags";          run_sql documents "$Q_TAGS"
    ;;
  -h|--help|help) sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//' ;;
  *) echo "Commande inconnue : $1 (voir $0 help)" >&2; exit 1 ;;
esac
