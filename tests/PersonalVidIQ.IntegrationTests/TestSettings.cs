namespace PersonalVidIQ.IntegrationTests;

internal static class TestSettings
{
    /// <summary>
    /// Integration tests use a separate local database. Override with PERSONALVIDIQ_TEST_DB.
    /// Create it once with: createdb personalvidiq_test
    /// </summary>
    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("PERSONALVIDIQ_TEST_DB")
        ?? "Host=localhost;Port=5432;Database=personalvidiq_test";
}
