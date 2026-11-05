-- Charging's read models, the projections' offsets and the journal-as-outbox (Chapters 13, 14
-- and 16), until section 20.8 created by ReadModelSchema when the first projection started.
-- Written with IF NOT EXISTS so that it adopts a database where that code already made them.

CREATE SCHEMA IF NOT EXISTS read_models;

CREATE TABLE IF NOT EXISTS read_models.projection_offsets (
    projection_id text        NOT NULL,
    tag           text        NOT NULL,
    last_offset   bigint      NOT NULL,
    updated_at    timestamptz NOT NULL DEFAULT now(),
    last_event_at timestamptz NULL,
    PRIMARY KEY (projection_id, tag)
);

-- Section 14.3 added the column to a table that already existed.
ALTER TABLE read_models.projection_offsets ADD COLUMN IF NOT EXISTS last_event_at timestamptz NULL;

CREATE TABLE IF NOT EXISTS read_models.projection_failures (
    projection_id  text        NOT NULL,
    persistence_id text        NOT NULL,
    sequence_nr    bigint      NOT NULL,
    error          text        NOT NULL,
    failed_at      timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (projection_id, persistence_id, sequence_nr)
);

CREATE TABLE IF NOT EXISTS read_models.session_history (
    session_id       uuid        PRIMARY KEY,
    token_uid        text        NOT NULL,
    connector_id     text        NOT NULL,
    status           text        NOT NULL,
    energy_kwh       numeric     NOT NULL,
    meter_start_wh   bigint      NOT NULL,
    started_at       timestamptz NOT NULL,
    ended_at         timestamptz NULL,
    last_sequence_nr bigint      NOT NULL
);

CREATE INDEX IF NOT EXISTS session_history_by_token
    ON read_models.session_history (token_uid, started_at DESC);

CREATE TABLE IF NOT EXISTS read_models.site_live_board (
    connector_id           text    PRIMARY KEY,
    site_id                text    NOT NULL,
    status                 text    NOT NULL DEFAULT 'unknown',
    session_id             uuid    NULL,
    status_sequence_nr     bigint  NOT NULL DEFAULT 0,
    allocated_kw           numeric NULL,
    allocation_sequence_nr bigint  NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS site_live_board_by_site
    ON read_models.site_live_board (site_id);

CREATE TABLE IF NOT EXISTS read_models.driver_app_cards (
    token_uid                 text        NOT NULL,
    session_id                uuid        NOT NULL,
    status                    text        NOT NULL,
    connector_id              text        NOT NULL,
    charge_point_display_name text        NULL,
    energy_kwh                numeric     NOT NULL,
    meter_start_wh            bigint      NOT NULL,
    started_at                timestamptz NOT NULL,
    ended_at                  timestamptz NULL,
    version                   bigint      NOT NULL,
    PRIMARY KEY (token_uid, session_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS driver_app_cards_by_session
    ON read_models.driver_app_cards (session_id);

CREATE INDEX IF NOT EXISTS driver_app_cards_by_token
    ON read_models.driver_app_cards (token_uid, started_at DESC);

CREATE TABLE IF NOT EXISTS read_models.driver_app_feed (
    position   bigserial PRIMARY KEY,
    token_uid  text      NOT NULL,
    session_id uuid      NOT NULL,
    version    bigint    NOT NULL,
    payload    jsonb     NOT NULL
);

CREATE INDEX IF NOT EXISTS driver_app_feed_by_token
    ON read_models.driver_app_feed (token_uid, position);

-- Section 16.1: the journal is the outbox; the relay writes integration events here.
CREATE TABLE IF NOT EXISTS read_models.integration_outbox (
    position    bigserial   PRIMARY KEY,
    event_id    uuid        NOT NULL UNIQUE,
    type        text        NOT NULL,
    occurred_at timestamptz NOT NULL,
    payload     jsonb       NOT NULL,
    written_at  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS read_models.integration_relay_sessions (
    session_id   uuid        PRIMARY KEY,
    token_uid    text        NOT NULL,
    connector_id text        NOT NULL,
    started_at   timestamptz NOT NULL,
    currency     text        NOT NULL,
    energy_rate  numeric     NOT NULL,
    session_fees numeric     NOT NULL
);
