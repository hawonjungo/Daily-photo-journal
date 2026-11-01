using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Relifes.Api.Auth;
using Relifes.Api.Data;
using Relifes.Api.Entities;
using Relifes.Api.Places;
using Relifes.Api.Storage;
using Testcontainers.PostgreSql;

namespace Relifes.Api.Tests;

// One shared database and sequential classes: Program.cs reads process-wide environment variables,
// and the Google call counter is global.
[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "Api";
}

public class FakePlacesClient : IPlacesClient
{
    public int CallCount;
    public Func<double, double, IReadOnlyList<ExternalPlace>> Respond { get; set; } = (_, _) => [];

    public Task<IReadOnlyList<ExternalPlace>> SearchNearbyAsync(
        double lat,
        double lng,
        double radiusMeters,
        int maxResults,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref CallCount);
        return Task.FromResult(Respond(lat, lng));
    }
}

public class ApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const int DailyGoogleCap = 5;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public FakeImageStorage Storage { get; } = new();
    public FakePlacesClient PlacesClient { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Program.cs reads these before WebApplicationFactory configuration callbacks run.
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("R2__ServiceUrl", "https://r2.invalid");
        Environment.SetEnvironmentVariable("R2__AccessKeyId", "test");
        Environment.SetEnvironmentVariable("R2__SecretAccessKey", "test");
        Environment.SetEnvironmentVariable("R2__BucketName", "test");
        Environment.SetEnvironmentVariable("Places__GoogleApiKey", "test");
        Environment.SetEnvironmentVariable("Places__DailyGoogleCap", DailyGoogleCap.ToString());

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IImageStorage>(Storage);
            services.AddSingleton<IPlacesClient>(PlacesClient);
        });
    }

    public async Task<(Guid Id, string ApiKey)> CreateUserAsync()
    {
        var key = ApiKey.Generate();
        var user = new User
        {
            Email = $"{Guid.NewGuid()}@example.com",
            DisplayName = "Test",
            ApiKeyHash = ApiKey.Hash(key),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await WithDbAsync(async db =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        });

        return (user.Id, key);
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

public class FakeImageStorage : IImageStorage
{
    public ConcurrentDictionary<string, byte[]> Objects { get; } = new();
    public string? FailUploadsEndingWith { get; set; }

    public Task UploadAsync(string key, byte[] content, string contentType, CancellationToken cancellationToken)
    {
        if (FailUploadsEndingWith is not null && key.EndsWith(FailUploadsEndingWith))
        {
            throw new IOException("Simulated storage failure");
        }

        Objects[key] = content;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        Objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<string> GetDownloadUrlAsync(string key, TimeSpan lifetime) =>
        Task.FromResult($"https://fake-storage/{key}?signed=true");
}
