namespace DPV.Domain.Contracts.Catalog;

public interface IDbMaintenanceService
{
    Task VacuumDatabaseAsync(CancellationToken cancellationToken = default);

    Task BackupDatabaseAsync(string destinationFilePath, CancellationToken cancellationToken = default);

    Task RestoreDatabaseAsync(string sourceFilePath, CancellationToken cancellationToken = default);
}
