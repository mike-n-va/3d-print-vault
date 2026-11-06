using DPV.App.Startup;
using DPV.Infrastructure.Persistence;
using DPV.Tests.Integration.DependencyInjection;
using DPV.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DPV.Tests.Integration.Persistence;

[Collection(nameof(CompositionRootIntegrationCollection))]
public class SqliteBootstrapIntegrationTests
{
    [Fact]
    public async Task HostStartupCreatesDatabaseAndEnablesRequiredSqliteSettings()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var databaseDirectory = Path.Combine(temporaryDirectory, "database");
        var databasePath = Path.Combine(databaseDirectory, "catalog.db");

        try
        {
            using (var host = BuildHost(databasePath, temporaryDirectory))
            {
                Assert.False(Directory.Exists(databaseDirectory));
                await host.StartAsync(TestContext.Current.CancellationToken);

                var factory = host.Services.GetRequiredService<IDatabaseConnectionFactory>();
                Assert.Equal(Path.GetFullPath(databasePath), factory.DatabasePath);
                Assert.True(File.Exists(factory.DatabasePath));

                await using var connection = factory.CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                Assert.Equal(1L, await ExecuteScalarAsync(connection, "PRAGMA foreign_keys;"));
                Assert.Equal("wal", await ExecuteStringScalarAsync(connection, "PRAGMA journal_mode;"));
                Assert.Equal(
                    (long)MigrationRunner.CurrentSchemaVersion,
                    await ExecuteScalarAsync(connection, "PRAGMA user_version;"));
                Assert.Equal(
                    1L,
                    await ExecuteScalarAsync(
                        connection,
                        "SELECT COUNT(*) FROM DPVSchemaMigrations WHERE Version = 1;"));

                await host.StopAsync(TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            CompositionRootTestIsolation.ResetLogging();
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public async Task MigrationBaselineRemainsIdempotentAcrossHostRestarts()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(temporaryDirectory, "catalog.db");

        try
        {
            using (var firstHost = BuildHost(databasePath, temporaryDirectory))
            {
                await firstHost.StartAsync(TestContext.Current.CancellationToken);
                await firstHost.StopAsync(TestContext.Current.CancellationToken);
            }

            using (var secondHost = BuildHost(databasePath, temporaryDirectory))
            {
                await secondHost.StartAsync(TestContext.Current.CancellationToken);
                var factory = secondHost.Services.GetRequiredService<IDatabaseConnectionFactory>();

                await using var connection = factory.CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                Assert.Equal(
                    (long)MigrationRunner.CurrentSchemaVersion,
                    await ExecuteScalarAsync(connection, "PRAGMA user_version;"));
                Assert.Equal(
                    (long)MigrationRunner.CurrentSchemaVersion,
                    await ExecuteScalarAsync(connection, "SELECT COUNT(*) FROM DPVSchemaMigrations;"));

                await secondHost.StopAsync(TestContext.Current.CancellationToken);
            }
        }
        finally
        {
            CompositionRootTestIsolation.ResetLogging();
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public async Task HostStartupRejectsDatabaseWithNewerSchemaVersion()
    {
        var temporaryDirectory = CreateTemporaryDirectory();
        var databasePath = Path.Combine(temporaryDirectory, "catalog.db");

        try
        {
            var factory = new SqliteConnectionFactory(databasePath);
            await using (var connection = factory.CreateConnection())
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = $"PRAGMA user_version = {MigrationRunner.CurrentSchemaVersion + 1};";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            using var host = BuildHost(databasePath, temporaryDirectory);
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => host.StartAsync(TestContext.Current.CancellationToken));

            Assert.Contains("newer than the supported version", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            CompositionRootTestIsolation.ResetLogging();
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    private static IHost BuildHost(string databasePath, string temporaryDirectory)
    {
        CompositionRootTestIsolation.ResetLogging();
        var builder = CompositionRootHostFactory.CreateHostBuilder([]);
        builder.Environment.ContentRootPath = TestRepository.AppProjectDirectory;
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Application:ApplicationName"] = "Issue 8 SQLite integration test",
            ["Database:Location"] = databasePath,
            ["Serilog:WriteTo:1:Args:path"] = Path.Combine(
                temporaryDirectory,
                "logs",
                "dpv-.log")
        });

        return builder.Build();
    }

    private static async Task<long> ExecuteScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<string?> ExecuteStringScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dpv-issue-8-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
