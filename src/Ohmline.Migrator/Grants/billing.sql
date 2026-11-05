-- Run after every migration (section 20.8): Billing's role may read and write the rows of its
-- tables, and nothing else. A login for the service is a member of this role.

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'billing_app') THEN
        CREATE ROLE billing_app NOLOGIN;
    END IF;
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO billing_app', current_database());
END
$$;

GRANT USAGE ON SCHEMA billing TO billing_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA billing TO billing_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA billing TO billing_app;
