CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS document_chunks (
    id uuid PRIMARY KEY,
    book_id uuid NOT NULL,
    chunk_index integer NOT NULL,
    page_number integer NOT NULL,
    content text NOT NULL,
    embedding vector(@@EMBEDDING_DIMENSIONS@@) NOT NULL,
    embedding_provider text NOT NULL,
    embedding_model text NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_document_chunks_book_id
    ON document_chunks (book_id);

CREATE INDEX IF NOT EXISTS ix_document_chunks_embedding_cosine
    ON document_chunks USING hnsw (embedding vector_cosine_ops);
