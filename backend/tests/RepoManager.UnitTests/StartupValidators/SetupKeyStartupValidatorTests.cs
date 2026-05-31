using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using RepoManager.Api.StartupValidators;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.UnitTests.StartupValidators;

public class SetupKeyStartupValidatorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public SetupKeyStartupValidatorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private SetupKeyStartupValidator CreateValidator(
        string? configuredKey,
        out Mock<IHostApplicationLifetime> lifetimeMock,
        out Mock<ILogger<SetupKeyStartupValidator>> loggerMock)
    {
        var dict = configuredKey is not null
            ? new Dictionary<string, string?> { ["RELEASE_MANAGER_SETUP_KEY"] = configuredKey }
            : new Dictionary<string, string?>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        lifetimeMock = new Mock<IHostApplicationLifetime>();
        loggerMock = new Mock<ILogger<SetupKeyStartupValidator>>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(sp => sp.GetService(typeof(AppDbContext)))
            .Returns(_db);

        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        return new SetupKeyStartupValidator(
            config,
            scopeFactoryMock.Object,
            lifetimeMock.Object,
            loggerMock.Object);
    }

    private void SeedUser()
    {
        _db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            PasswordHash = "hash",
            Role = Role.Admin,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task StartAsync_KeyAbsentAndNoUsers_CallsStopApplication()
    {
        var validator = CreateValidator(
            configuredKey: null,
            out var lifetimeMock,
            out _);

        await validator.StartAsync(CancellationToken.None);

        lifetimeMock.Verify(l => l.StopApplication(), Times.Once);
    }

    [Fact]
    public async Task StartAsync_KeyAbsentAndNoUsers_LogsFatalMessage()
    {
        var validator = CreateValidator(
            configuredKey: null,
            out _,
            out var loggerMock);

        await validator.StartAsync(CancellationToken.None);

        loggerMock.Verify(
            l => l.Log(
                LogLevel.Critical,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains("RELEASE_MANAGER_SETUP_KEY")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task StartAsync_KeyAbsentButUsersExist_DoesNotCallStopApplication()
    {
        SeedUser();
        var validator = CreateValidator(
            configuredKey: null,
            out var lifetimeMock,
            out _);

        await validator.StartAsync(CancellationToken.None);

        lifetimeMock.Verify(l => l.StopApplication(), Times.Never);
    }

    [Fact]
    public async Task StartAsync_KeyPresent_DoesNotCallStopApplicationEvenWithNoUsers()
    {
        var validator = CreateValidator(
            configuredKey: "any-key-value-here",
            out var lifetimeMock,
            out _);

        await validator.StartAsync(CancellationToken.None);

        lifetimeMock.Verify(l => l.StopApplication(), Times.Never);
    }
}
