using IntegrationTests.Infrastructure;

namespace IntegrationTests;

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<TestDatabaseFixture>
{
    public const string Name = "Database";
}

public sealed class TestDatabaseFixture : IAsyncLifetime
{
    public ApiWebApplicationFactory Factory { get; } = new();

    public Task InitializeAsync()
    {
        _ = Factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        return Factory.DisposeAsync().AsTask();
    }
}