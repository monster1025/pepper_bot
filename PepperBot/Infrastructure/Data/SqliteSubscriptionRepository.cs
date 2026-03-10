using Microsoft.Data.Sqlite;
using PepperBot.Application.Interfaces;
using PepperBot.Domain;

namespace PepperBot.Infrastructure.Data;

public class SqliteSubscriptionRepository : ISubscriptionRepository
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
        if (!Directory.Exists(DataDirectory))
        {
            Directory.CreateDirectory(DataDirectory);
        }

        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS Subscriptions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId TEXT NOT NULL,
                Keywords TEXT NOT NULL,
                IsActive INTEGER NOT NULL DEFAULT 1
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> GetAllActiveAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ChatId, Keywords, IsActive
            FROM Subscriptions
            WHERE IsActive = 1;
            """;

        var result = new List<Subscription>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var subscription = new Subscription
            {
                Id = reader.GetInt64(0),
                ChatId = reader.GetString(1),
                Keywords = reader.GetString(2),
                IsActive = reader.GetInt32(3) == 1
            };

            result.Add(subscription);
        }

        return result;
    }

    public async Task AddSubscriptionAsync(string chatId, string keyword, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO Subscriptions (ChatId, Keywords, IsActive)
            VALUES ($chatId, $keywords, 1);
            """;

        command.Parameters.AddWithValue("$chatId", chatId);
        command.Parameters.AddWithValue("$keywords", keyword);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Subscription>> GetByChatAsync(string chatId, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT Id, ChatId, Keywords, IsActive
            FROM Subscriptions
            WHERE ChatId = $chatId AND IsActive = 1;
            """;

        command.Parameters.AddWithValue("$chatId", chatId);

        var result = new List<Subscription>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var subscription = new Subscription
            {
                Id = reader.GetInt64(0),
                ChatId = reader.GetString(1),
                Keywords = reader.GetString(2),
                IsActive = reader.GetInt32(3) == 1
            };

            result.Add(subscription);
        }

        return result;
    }

    public async Task DeleteSubscriptionAsync(long id, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE Subscriptions
            SET IsActive = 0
            WHERE Id = $id;
            """;

        command.Parameters.AddWithValue("$id", id);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

