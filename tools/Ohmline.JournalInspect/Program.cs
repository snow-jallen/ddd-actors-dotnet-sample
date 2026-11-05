using Ohmline.JournalInspect;

// dotnet run --project tools/Ohmline.JournalInspect -- \
//     --postgres "Host=localhost;Port=5432;Database=ohmline;Username=postgres;Password=..." \
//     --persistence-id charge-point-OHM-SLC-0042
// dotnet run --project tools/Ohmline.JournalInspect -- --sqlite src/Ohmline.Node/ohmline.db
if (InspectOptions.Parse(args) is not { } options)
{
    Console.Error.WriteLine(InspectOptions.Usage);
    return 2;
}

await using var connection = options.Connect();
await connection.OpenAsync();
foreach (var row in await options.ReadAsync(new JournalReader(connection), CancellationToken.None))
{
    Console.WriteLine(RowFormat.Line(row));
}

return 0;
