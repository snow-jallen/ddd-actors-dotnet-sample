using Npgsql;

namespace Ohmline.Billing;

/// <summary>
/// Billing's tables, in the schema <c>billing</c> of its own database: state stored as rows,
/// the current truth and nothing else (section 16.3). Created when the service starts if they
/// are missing, until Chapter 20's migrator owns them.
/// </summary>
public static class BillingSchema
{
    private const string Tables = """
        SELECT pg_advisory_xact_lock(hashtext('ohmline.billing'));

        CREATE SCHEMA IF NOT EXISTS billing;

        CREATE TABLE IF NOT EXISTS billing.holds (
            hold_id    text        PRIMARY KEY,
            driver_ref text        NOT NULL,
            amount     numeric     NOT NULL,
            currency   text        NOT NULL,
            status     text        NOT NULL,
            captured   numeric     NULL,
            created_at timestamptz NOT NULL,
            updated_at timestamptz NOT NULL
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
        """;

    public static async Task EnsureAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var create = new NpgsqlCommand(Tables, connection, transaction))
        {
            await create.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
