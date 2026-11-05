-- Akka.Persistence.Sql 1.5.73's published DDL for PostgreSQL (section 20.8): the file
-- docs/ddl/default/postgresql/journal.sql at the tag 1.5.73 of
-- github.com/akkadotnet/Akka.Persistence.Sql, unchanged below this header. It is idempotent,
-- so it also adopts the tables that autoInitialize made before Ohmline had a migrator.

-- Journal Table DDL
-- Generated for PostgreSQL (table-mapping = default)
-- This table stores all persisted events

CREATE TABLE IF NOT EXISTS "public"."journal" (
    "ordering" bigserial NOT NULL,
    "deleted" boolean NOT NULL DEFAULT false,
    "persistence_id" text NOT NULL,
    "sequence_number" bigint NOT NULL,
    "created" bigint NOT NULL,
    "tags" text,
    "message" bytea NOT NULL,
    "identifier" integer,
    "manifest" text,
    "writer_uuid" text,
    CONSTRAINT "PK_journal" PRIMARY KEY ("ordering")
);

-- Additional constraints and indexes:
DO $BLOCK$
BEGIN
    BEGIN
        ALTER TABLE "public"."journal" ADD CONSTRAINT journal_uq UNIQUE (persistence_id, sequence_number);
    EXCEPTION
        WHEN duplicate_table OR duplicate_object
        THEN RAISE NOTICE 'unique constraint "journal_uq" on "public"."journal" already exists, skipping';
    END;

    BEGIN
        CREATE INDEX journal_ordering_idx ON "public"."journal" (ordering);
    EXCEPTION
        WHEN duplicate_table
        THEN RAISE NOTICE 'index "journal_ordering_idx" on "public"."journal" already exists, skipping';
    END;

    BEGIN
        CREATE INDEX journal_created_idx ON "public"."journal" (created);
    EXCEPTION
        WHEN duplicate_table
        THEN RAISE NOTICE 'index "journal_created_idx" on "public"."journal" already exists, skipping';
    END;

    BEGIN
        CREATE INDEX journal_persistence_id_idx ON "public"."journal" (persistence_id);
    EXCEPTION
        WHEN duplicate_table
        THEN RAISE NOTICE 'index "journal_persistence_id_idx" on "public"."journal" already exists, skipping';
    END;
END;
$BLOCK$

