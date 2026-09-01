using Xunit;

namespace Foundgine.Testing;

/// <summary>Skips database tests when PostgreSQL is not configured/reachable.</summary>
public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgresFixture.ConnectionEnvironmentVariable)))
            Skip = "Set FOUNDGINE_POSTGRES_CONNECTION_STRING to run PostgreSQL integration tests.";
    }
}
