using Microsoft.Data.Sqlite;

namespace DPV.Infrastructure.Persistence;

public interface IDatabaseConnectionFactory
{
    string DatabasePath { get; }

    SqliteConnection CreateConnection();
}
