-- The session history's second version (section 20.9): a table of its own, which its
-- projection fills from the journal's first event while the first version serves reads.
-- The first version's table is dropped by a later script, once nothing reads it.

CREATE TABLE IF NOT EXISTS read_models.session_history_v2 (
    session_id       uuid        PRIMARY KEY,
    site_id          text        NOT NULL,
    token_uid        text        NOT NULL,
    connector_id     text        NOT NULL,
    status           text        NOT NULL,
    energy_kwh       numeric     NOT NULL,
    meter_start_wh   bigint      NOT NULL,
    phase_count      integer     NULL,
    started_at       timestamptz NOT NULL,
    ended_at         timestamptz NULL,
    last_sequence_nr bigint      NOT NULL
);

CREATE INDEX IF NOT EXISTS session_history_v2_by_token
    ON read_models.session_history_v2 (token_uid, started_at DESC);

CREATE INDEX IF NOT EXISTS session_history_v2_by_site
    ON read_models.session_history_v2 (site_id, started_at DESC);
