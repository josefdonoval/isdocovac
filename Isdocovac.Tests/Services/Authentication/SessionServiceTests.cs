using FluentAssertions;
using Isdocovac.Models;
using Isdocovac.Providers;
using Isdocovac.Services.Authentication;
using Isdocovac.Tests.Helpers;
using Isdocovac.Utils;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Isdocovac.Tests.Services.Authentication;

public class SessionServiceTests
{
    private readonly Mock<ISessionProvider> _sessionProviderMock;
    private readonly Mock<IUserProvider> _userProviderMock;
    private readonly IConfiguration _defaultConfig;
    private readonly SessionService _sut;

    public SessionServiceTests()
    {
        _sessionProviderMock = new Mock<ISessionProvider>();
        _userProviderMock = new Mock<IUserProvider>();
        _defaultConfig = TestHelpers.BuildConfiguration(new Dictionary<string, string?>());

        _sessionProviderMock
            .Setup(p => p.CreateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((Guid userId, string hash, DateTime exp, string? ip, string? ua) =>
                new UserSession
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    SessionTokenHash = hash,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = exp,
                    IpAddress = ip,
                    UserAgent = ua,
                    IsActive = true
                });

        _sut = new SessionService(_sessionProviderMock.Object, _userProviderMock.Object, _defaultConfig);
    }

    private SessionService CreateServiceWithConfig(Dictionary<string, string?> settings)
    {
        var config = TestHelpers.BuildConfiguration(settings);
        return new SessionService(_sessionProviderMock.Object, _userProviderMock.Object, config);
    }

    [Fact]
    public async Task CreateSessionAsync_ReturnsNonEmptyToken()
    {
        var userId = Guid.NewGuid();

        var token = await _sut.CreateSessionAsync(userId, "127.0.0.1", "TestAgent");

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CreateSessionAsync_CallsCreateAsyncWithHashedToken()
    {
        var userId = Guid.NewGuid();

        var plainToken = await _sut.CreateSessionAsync(userId, "127.0.0.1", "TestAgent");

        _sessionProviderMock.Verify(p => p.CreateAsync(
            userId,
            It.Is<string>(hash => hash != plainToken && hash == TokenHasher.HashToken(plainToken)),
            It.IsAny<DateTime>(),
            "127.0.0.1",
            "TestAgent"), Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_UsesConfiguredExpirationDays()
    {
        var service = CreateServiceWithConfig(new Dictionary<string, string?>
        {
            { "Authentication:Session:AbsoluteExpirationDays", "30" }
        });
        var userId = Guid.NewGuid();

        var before = DateTime.UtcNow.AddDays(30);
        await service.CreateSessionAsync(userId, null, null);
        var after = DateTime.UtcNow.AddDays(30);

        _sessionProviderMock.Verify(p => p.CreateAsync(
            userId,
            It.IsAny<string>(),
            It.Is<DateTime>(exp => exp >= before.AddSeconds(-1) && exp <= after.AddSeconds(1)),
            It.IsAny<string?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_UsesDefault14DayExpiration()
    {
        var userId = Guid.NewGuid();

        var before = DateTime.UtcNow.AddDays(14);
        await _sut.CreateSessionAsync(userId, null, null);
        var after = DateTime.UtcNow.AddDays(14);

        _sessionProviderMock.Verify(p => p.CreateAsync(
            userId,
            It.IsAny<string>(),
            It.Is<DateTime>(exp => exp >= before.AddSeconds(-1) && exp <= after.AddSeconds(1)),
            It.IsAny<string?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task CreateSessionAsync_UpdatesLastLogin()
    {
        var userId = Guid.NewGuid();

        await _sut.CreateSessionAsync(userId, null, null);

        _userProviderMock.Verify(p => p.UpdateLastLoginAsync(userId), Times.Once);
    }

    [Fact]
    public async Task ValidateSessionAsync_ReturnsNull_WhenSessionNotFound()
    {
        _sessionProviderMock
            .Setup(p => p.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync((UserSession?)null);

        var result = await _sut.ValidateSessionAsync("nonexistent-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSessionAsync_RevokesAndReturnsNull_WhenExpired()
    {
        var sessionId = Guid.NewGuid();
        var expiredSession = new UserSession
        {
            Id = sessionId,
            UserId = Guid.NewGuid(),
            SessionTokenHash = "hash",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            IsActive = true
        };

        _sessionProviderMock
            .Setup(p => p.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(expiredSession);

        var result = await _sut.ValidateSessionAsync("some-token");

        result.Should().BeNull();
        _sessionProviderMock.Verify(p => p.RevokeAsync(sessionId), Times.Once);
    }

    [Fact]
    public async Task ValidateSessionAsync_ReturnsNull_WhenNotActive()
    {
        var inactiveSession = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SessionTokenHash = "hash",
            CreatedAt = DateTime.UtcNow.AddDays(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(13),
            IsActive = false
        };

        _sessionProviderMock
            .Setup(p => p.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(inactiveSession);

        var result = await _sut.ValidateSessionAsync("some-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateSessionAsync_UpdatesLastActivity_OnSuccess()
    {
        var session = CreateValidSession();
        _sessionProviderMock
            .Setup(p => p.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(session);
        _sessionProviderMock
            .Setup(p => p.UpdateLastActivityAsync(session.Id))
            .ReturnsAsync(session);

        await _sut.ValidateSessionAsync("some-token");

        _sessionProviderMock.Verify(p => p.UpdateLastActivityAsync(session.Id), Times.Once);
    }

    [Fact]
    public async Task ValidateSessionAsync_ReturnsUser_OnSuccess()
    {
        var expectedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "test",
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var session = CreateValidSession(expectedUser);

        _sessionProviderMock
            .Setup(p => p.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(session);
        _sessionProviderMock
            .Setup(p => p.UpdateLastActivityAsync(session.Id))
            .ReturnsAsync(session);

        var result = await _sut.ValidateSessionAsync("some-token");

        result.Should().NotBeNull();
        result.Should().BeSameAs(expectedUser);
    }

    [Fact]
    public async Task RevokeSessionAsync_RevokesSession_WhenFound()
    {
        var session = CreateValidSession();
        _sessionProviderMock
            .Setup(p => p.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(session);

        await _sut.RevokeSessionAsync("some-token");

        _sessionProviderMock.Verify(p => p.RevokeAsync(session.Id), Times.Once);
    }

    [Fact]
    public async Task RevokeSessionAsync_DoesNothing_WhenNotFound()
    {
        _sessionProviderMock
            .Setup(p => p.GetBySessionTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync((UserSession?)null);

        await _sut.RevokeSessionAsync("nonexistent-token");

        _sessionProviderMock.Verify(p => p.RevokeAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task RevokeAllUserSessionsAsync_DelegatesToProvider()
    {
        var userId = Guid.NewGuid();

        await _sut.RevokeAllUserSessionsAsync(userId);

        _sessionProviderMock.Verify(p => p.RevokeAllByUserAsync(userId), Times.Once);
    }

    [Fact]
    public async Task CleanupExpiredSessionsAsync_DelegatesToProvider()
    {
        _sessionProviderMock
            .Setup(p => p.CleanupExpiredAsync())
            .ReturnsAsync(3);

        var result = await _sut.CleanupExpiredSessionsAsync();

        result.Should().Be(3);
        _sessionProviderMock.Verify(p => p.CleanupExpiredAsync(), Times.Once);
    }

    private static UserSession CreateValidSession(User? user = null)
    {
        var sessionUser = user ?? new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "test",
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        return new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = sessionUser.Id,
            SessionTokenHash = "hash",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            ExpiresAt = DateTime.UtcNow.AddDays(13),
            IsActive = true,
            User = sessionUser
        };
    }
}
