using Microsoft.Data.Sqlite;
using Mkr.Core;
using Mkr.Infrastructure;

var directory = Directory.CreateTempSubdirectory("mkr-test-");
try
{
    var path = Path.Combine(directory.FullName, "test.db");
    var store = new SqliteProjectStore(path);
    Require(store.List().Count == 0, "The migrated database is empty.");
    var project = Project.Create("  Banc d’essai  ");
    Require(project.Name == "Banc d’essai", "Project name is trimmed.");
    Throws<ArgumentException>(() => Project.Create(" "));
    Throws<ArgumentException>(() => Project.Create(new string('x', 121)));

    store.Add(project);
    var reopened = new SqliteProjectStore(path);
    Require(reopened.List().Single() == project, "Project survives reopening and migration replay.");
    Throws<SqliteException>(() => reopened.Add(project));
    Require(reopened.List().Single() == project, "Duplicate insert preserves the original project.");

    using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = path, Pooling = false
    }.ToString()))
    {
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version = 2";
        command.ExecuteNonQuery();
    }
    Throws<InvalidDataException>(() => new SqliteProjectStore(path));

    var interpreter = Environment.GetEnvironmentVariable("MKR_PYTHON") ?? "python";
    var engine = new PythonDiagnostics(interpreter, Path.Combine(AppContext.BaseDirectory, "engine", "mkr_engine.py"));
    Require(await engine.GetVersionAsync(default) == "0.1.0", "Python round trip returns the expected version.");

    using var cancellation = new CancellationTokenSource();
    cancellation.Cancel();
    try
    {
        await engine.GetVersionAsync(cancellation.Token);
        throw new InvalidOperationException("Expected cancellation.");
    }
    catch (OperationCanceledException) { }

    Console.WriteLine("Project validation, SQLite migration/persistence, IPC round trip and pre-cancellation passed.");
}
finally
{
    directory.Delete(recursive: true);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void Throws<T>(Action operation) where T : Exception
{
    try { operation(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
