#!/usr/bin/env bash
set -euo pipefail

embedding_dimensions="${EMBEDDING_DIMENSIONS:-768}"
if [[ ! "$embedding_dimensions" =~ ^[1-9][0-9]*$ ]] || (( embedding_dimensions > 2000 )); then
    echo "EMBEDDING_DIMENSIONS must be between 1 and 2000 for a pgvector HNSW index." >&2
    exit 1
fi

sed "s/@@EMBEDDING_DIMENSIONS@@/${embedding_dimensions}/g" /opt/DocumentChunks.sql \
    | psql --set=ON_ERROR_STOP=1 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB"
