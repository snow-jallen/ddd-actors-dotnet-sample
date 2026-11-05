-- Run after every migration (section 20.8): the node's role may read and write the rows of
-- every table the scripts made, and nothing else. It cannot create, alter or drop a table, so
-- a node with autoInitialize left on by mistake fails at once instead of changing the schema.
-- A login for the node is a member of this role: CREATE ROLE ... LOGIN IN ROLE ohmline_app.

DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'ohmline_app') THEN
        CREATE ROLE ohmline_app NOLOGIN;
    END IF;
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO ohmline_app', current_database());
END
$$;

GRANT USAGE ON SCHEMA public, read_models TO ohmline_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public, read_models TO ohmline_app;
GRANT USAGE, SELECT, UPDATE ON ALL SEQUENCES IN SCHEMA public, read_models TO ohmline_app;

-- The migrator's own record of what it applied is not the node's business.
REVOKE ALL ON TABLE public.schemaversions FROM ohmline_app;
