namespace OpenGate.Application.Migration;

public interface IMigrationService
{
    Task<ConnectionTestResult> TestConnectionAsync(string connectionString);
    Task<MigrationProgress> RunMigrationAsync(string connectionString);
    MigrationProgress GetProgress();
}
