using Microsoft.Data.Sqlite;
using PepperBot.Application.Interfaces;
using PepperBot.Domain;

namespace PepperBot.Infrastructure.Data;

public class SqliteDealRepository : IDealRepository
{
    private const string DatabaseFileName = "pepper_bot.db";
    private const string DataDirectory = "data";

    private string ConnectionString =>
        new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(DataDirectory, DatabaseFileName)
        }.ToString();

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (Directory.Exists(DataDirectory))
        {
            Directory.CreateDirectory(DataDirectory);
        }

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Deals (
                Id INTEGER PRIMARY KEY,
                Title TEXT NOT NULL,
                CurrentPrice REAL NULL,
                RetailPrice REAL NULL,
                PercentOff INTEGER NULL,
                CreatedAt TEXT NOT NULL,
                CreatedAtInMillis INTEGER NOT NULL,
                DealUrl TEXT NOT NULL,
                StoreName TEXT NOT NULL,
                StorePermalink TEXT NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM Deals WHERE Id = $id LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    public async Task AddAsync(Deal deal, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Deals
                (Id, Title, CurrentPrice, RetailPrice, PercentOff, CreatedAt, CreatedAtInMillis, DealUrl, StoreName, StorePermalink)
            VALUES
                ($id, $title, $currentPrice, $retailPrice, $percentOff, $createdAt, $createdAtInMillis, $dealUrl, $storeName, $storePermalink);
            """;

        command.Parameters.AddWithValue("$id", deal.Id);
        command.Parameters.AddWithValue("$title", deal.Title);
        command.Parameters.AddWithValue("$currentPrice", (object?)deal.CurrentPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("$retailPrice", (object?)deal.RetailPrice ?? DBNull.Value);
        command.Parameters.AddWithValue("$percentOff", (object?)deal.PercentOff ?? DBNull.Value);
        command.Parameters.AddWithValue("$createdAt", deal.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$createdAtInMillis", deal.CreatedAtInMillis);
        command.Parameters.AddWithValue("$dealUrl", deal.DealUrl);
        command.Parameters.AddWithValue("$storeName", deal.StoreName);
        command.Parameters.AddWithValue("$storePermalink", deal.StorePermalink);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

