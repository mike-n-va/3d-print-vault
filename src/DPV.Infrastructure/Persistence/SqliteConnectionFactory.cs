using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory : IDatabaseConnectionFactory
{
    private const string ApplicationDataToken = "%AppData%";
    private const string DatabaseFileName = "3d-print-vault.db";
    private readonly string _connectionString;

    public SqliteConnectionFactory(string location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(location);

        var expandedLocation = ExpandLocation(location);
        if (EndsWithDirectorySeparator(expandedLocation))
        {
            expandedLocation = Path.Combine(expandedLocation, DatabaseFileName);
        }

        DatabasePath = Path.GetFullPath(expandedLocation);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = false
        }.ToString();
    }

    public string DatabasePath { get; }

    public SqliteConnection CreateConnection() => new(_connectionString);

    private static string ExpandLocation(string location)
    {
        var expanded = Environment.ExpandEnvironmentVariables(location);
        if (expanded.Contains(ApplicationDataToken, StringComparison.OrdinalIgnoreCase))
        {
            var applicationDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(applicationDataPath))
            {
                throw new InvalidOperationException(
                    "The application data directory is unavailable for the configured database location.");
            }

            expanded = expanded.Replace(
                ApplicationDataToken,
                applicationDataPath,
                StringComparison.OrdinalIgnoreCase);
        }

        return expanded;
    }

    private static bool EndsWithDirectorySeparator(string path) =>
        path.EndsWith(Path.DirectorySeparatorChar)
        || path.EndsWith(Path.AltDirectorySeparatorChar);
}
