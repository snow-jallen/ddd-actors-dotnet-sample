-- Billing's tables, in the schema billing of its own database (section 16.3), until section
-- 20.8 created by BillingSchema when the service started. Written with IF NOT EXISTS so that
-- it adopts a database where that code already made them.

CREATE SCHEMA IF NOT EXISTS billing;

CREATE TABLE IF NOT EXISTS billing.holds (
    hold_id            text        PRIMARY KEY,
    driver_ref         text        NOT NULL,
    amount             numeric     NOT NULL,
    currency           text        NOT NULL,
    status             text        NOT NULL,
    captured           numeric     NULL,
    created_at         timestamptz NOT NULL,
    updated_at         timestamptz NOT NULL,
    provider_reference text        NULL
);

-- Section 16.4: TallyPay's reference for the authorization behind the hold.
ALTER TABLE billing.holds ADD COLUMN IF NOT EXISTS provider_reference text NULL;

CREATE TABLE IF NOT EXISTS billing.invoices (
    session_id   uuid        PRIMARY KEY,
    token_uid    text        NOT NULL,
    connector_id text        NOT NULL,
    energy_kwh   numeric     NOT NULL,
    started_at   timestamptz NOT NULL,
    ended_at     timestamptz NOT NULL,
    currency     text        NOT NULL,
    total        numeric     NOT NULL,
    lines        jsonb       NOT NULL,
    issued_at    timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS billing.inbox_position (
    feed     text   PRIMARY KEY,
    position bigint NOT NULL
);

CREATE TABLE IF NOT EXISTS billing.outbox (
    id         bigserial   PRIMARY KEY,
    type       text        NOT NULL,
    payload    jsonb       NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    sent_at    timestamptz NULL
);
