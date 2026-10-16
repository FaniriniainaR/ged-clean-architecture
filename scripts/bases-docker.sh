#!/usr/bin/env bash
# Démarre / arrête les bases MySQL et SQL Server de démonstration (Docker).
#
#   scripts/bases-docker.sh start    démarre les deux bases et crée la base « ged » dans SQL Server
#   scripts/bases-docker.sh stop     arrête les bases (les données sont perdues)
#   scripts/bases-docker.sh status   état des conteneurs
set -euo pipefail
cd "$(dirname "$0")/.."

case "${1:-}" in
  start)
    docker compose up -d
    echo "Attente du démarrage des bases…"
    for _ in $(seq 1 60); do
      if docker compose exec -T mysql mysqladmin ping -uroot -proot --silent >/dev/null 2>&1 &&
         docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Ged_Passw0rd!' -C \
           -Q "SELECT 1" >/dev/null 2>&1; then
        docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'Ged_Passw0rd!' -C \
          -Q "IF DB_ID('ged') IS NULL CREATE DATABASE ged" >/dev/null
        echo "Bases prêtes : MySQL (port 3306) et SQL Server (port 1433)."
        exit 0
      fi
      sleep 3
    done
    echo "Les bases n'ont pas démarré à temps. Voir : docker compose logs" >&2
    exit 1
    ;;
  stop)   docker compose down ;;
  status) docker compose ps ;;
  *) sed -n '2,6p' "$0" | sed 's/^# \{0,1\}//'; exit 1 ;;
esac
