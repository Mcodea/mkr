using System.Globalization;
using Microsoft.Data.Sqlite;
using Mkr.Core;

namespace Mkr.Infrastructure;

public sealed class SqliteProjectStore : IProjectStore
{
    private readonly string _connectionString;

    public SqliteProjectStore(string path)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "PRAGMA user_version";
        var version = (long)command.ExecuteScalar()!;
        switch (version)
        {
            case 0:
                command.CommandText = """
                    CREATE TABLE projects (
                        id TEXT PRIMARY KEY NOT NULL,
                        name TEXT NOT NULL,
                        created_at TEXT NOT NULL
                    );
                    PRAGMA user_version = 1;
                    """;
                command.ExecuteNonQuery();
                break;
            case 1:
                break;
            default:
                throw new InvalidDataException($"Version SQLite MKR non prise en charge : {version}.");
        }

        transaction.Commit();
    }

    public IReadOnlyList<Project> List()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, created_at FROM projects ORDER BY created_at, id";
        using var reader = command.ExecuteReader();
        var projects = new List<Project>();
        while (reader.Read())
            projects.Add(new Project(Guid.Parse(reader.GetString(0)), reader.GetString(1),
                DateTimeOffset.ParseExact(reader.GetString(2), "O", CultureInfo.InvariantCulture)));
        return projects;
    }

    public void Add(Project project)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO projects (id, name, created_at) VALUES ($id, $name, $created)";
        command.Parameters.AddWithValue("$id", project.Id.ToString());
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$created", project.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
