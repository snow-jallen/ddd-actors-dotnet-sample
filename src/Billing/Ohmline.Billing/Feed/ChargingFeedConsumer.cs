using Microsoft.Extensions.Options;
using Npgsql;
using Ohmline.Billing.Invoicing;

namespace Ohmline.Billing.Feed;

/// <summary>
/// Reads Charging's feed from the position Billing stored last, for as long as Billing runs
/// (section 16.3): a page at a time, each entry handled and its position stored in one database
/// transaction, then the next page. With nothing new, it waits and asks again; when Charging or
/// the database fails, it waits longer and asks again from the stored position.
/// </summary>
public sealed class ChargingFeedConsumer(
    ChargingFeedClient feed,
    InvoiceIssuer invoices,
    NpgsqlDataSource database,
    IOptions<ChargingFeedOptions> options,
    TimeProvider clock,
    ILogger<ChargingFeedConsumer> log)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var pause = options.Value.PollInterval;
            try
            {
                if (await ReadPageAsync(stoppingToken) > 0)
                {
                    continue;
                }
            }
            catch (Exception failure) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogWarning(failure, "Reading the charging feed failed; trying again later");
                pause = options.Value.RetryInterval;
            }

            await Task.Delay(pause, clock, stoppingToken);
        }
    }

    /// <summary>Handles the page after the stored position; returns how many entries.</summary>
    public async Task<int> ReadPageAsync(CancellationToken cancellationToken)
    {
        var position = await Inbox.PositionAsync(database, Inbox.ChargingFeed, cancellationToken);
        var page = await feed.PageAfterAsync(position, options.Value.PageSize, cancellationToken);
        foreach (var item in ChargingFeedReader.Read(page))
        {
            await invoices.HandleAsync(item, cancellationToken);
        }

        return page.Entries.Length;
    }
}
