using FluentAssertions;
using Isdocovac.Models;
using Isdocovac.Providers;
using Isdocovac.Services.Authentication;
using Isdocovac.Tests.Helpers;
using Isdocovac.Utils;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Isdocovac.Tests.Services.Authentication;

public class MagicLinkServiceTests
{
    private readonly Mock<IAuthTokenProvider> _authTokenProviderMock;
    private readonly Mock<IUserProvider> _userProviderMock;
    private readonly IConfiguration _defaultConfig;
    private readonly MagicLinkService _sut;

    public MagicLinkServiceTests()
    {
        _authTokenProviderMock = new Mock<IAuthTokenProvider>();
        _userProviderMock = new Mock<IUserProvider>();
        _defaultConfig = TestHelpers.BuildConfiguration(new Dictionary<string, string?>());

        _authTokenProviderMock
            .Setup(p => p.CreateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync((string email, string hash, DateTime exp, string? ip, string? ua) =>
                new AuthenticationToken
                {
                    Id = Guid.NewGuid(),
                    Email = email,
                    TokenHash = hash,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = exp,
                    IpAddress = ip,
                    UserAgent = ua,
                    IsRevoked = false
                });

        _sut = new MagicLinkService(_authTokenProviderMock.Object, _userProviderMock.Object, _defaultConfig);
    }

    private MagicLinkService CreateServiceWithConfig(Dictionary<string, string?> settings)
    {
        var config = TestHelpers.BuildConfiguration(settings);
        return new MagicLinkService(_authTokenProviderMock.Object, _userProviderMock.Object, config);
    }

    [Fact]
    public async Task GenerateTokenAsync_ReturnsNonEmptyToken()
    {
        var token = await _sut.GenerateTokenAsync("test@example.com", "127.0.0.1", "TestAgent");

        token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GenerateTokenAsync_CallsCreateAsyncWithHashedToken_NotPlainToken()
    {
        var plainToken = await _sut.GenerateTokenAsync("test@example.com", "127.0.0.1", "TestAgent");

        _authTokenProviderMock.Verify(p => p.CreateAsync(
            "test@example.com",
            It.Is<string>(hash => hash != plainToken && hash == TokenHasher.HashToken(plainToken)),
            It.IsAny<DateTime>(),
            "127.0.0.1",
            "TestAgent"), Times.Once);
    }

    [Fact]
    public async Task GenerateTokenAsync_UsesConfiguredExpiration()
    {
        var service = CreateServiceWithConfig(new Dictionary<string, string?>
        {
            { "Authentication:MagicLink:ExpirationMinutes", "30" }
        });

        var before = DateTime.UtcNow.AddMinutes(30);
        await service.GenerateTokenAsync("test@example.com", null, null);
        var after = DateTime.UtcNow.AddMinutes(30);

        _authTokenProviderMock.Verify(p => p.CreateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<DateTime>(exp => exp >= before.AddSeconds(-1) && exp <= after.AddSeconds(1)),
            It.IsAny<string?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task GenerateTokenAsync_UsesDefault15MinExpiration_WhenNotConfigured()
    {
        var before = DateTime.UtcNow.AddMinutes(15);
        await _sut.GenerateTokenAsync("test@example.com", null, null);
        var after = DateTime.UtcNow.AddMinutes(15);

        _authTokenProviderMock.Verify(p => p.CreateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.Is<DateTime>(exp => exp >= before.AddSeconds(-1) && exp <= after.AddSeconds(1)),
            It.IsAny<string?>(),
            It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_WhenTokenNotFound()
    {
        _authTokenProviderMock
            .Setup(p => p.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync((AuthenticationToken?)null);

        var result = await _sut.ValidateTokenAsync("nonexistent-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_WhenTokenExpired()
    {
        var expiredToken = new AuthenticationToken
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            TokenHash = "hash",
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            IsRevoked = false
        };

        _authTokenProviderMock
            .Setup(p => p.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(expiredToken);

        var result = await _sut.ValidateTokenAsync("some-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_WhenTokenConsumed()
    {
        var consumedToken = new AuthenticationToken
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            TokenHash = "hash",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            ConsumedAt = DateTime.UtcNow.AddMinutes(-1),
            IsRevoked = false
        };

        _authTokenProviderMock
            .Setup(p => p.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(consumedToken);

        var result = await _sut.ValidateTokenAsync("some-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsNull_WhenTokenRevoked()
    {
        var revokedToken = new AuthenticationToken
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            TokenHash = "hash",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            IsRevoked = true
        };

        _authTokenProviderMock
            .Setup(p => p.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(revokedToken);

        var result = await _sut.ValidateTokenAsync("some-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTokenAsync_MarksTokenAsConsumed_OnSuccess()
    {
        var tokenId = Guid.NewGuid();
        var validToken = CreateValidAuthToken(tokenId);

        SetupValidTokenScenario(validToken);

        await _sut.ValidateTokenAsync("some-token");

        _authTokenProviderMock.Verify(p => p.MarkConsumedAsync(tokenId), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_CreatesOrGetsUserByEmail()
    {
        var validToken = CreateValidAuthToken();

        SetupValidTokenScenario(validToken);

        await _sut.ValidateTokenAsync("some-token");

        _userProviderMock.Verify(p => p.GetOrCreateByEmailAsync("test@example.com", null, null), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_MarksEmailAsVerified_WhenNotVerified()
    {
        var validToken = CreateValidAuthToken();
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Username = "test",
            EmailVerified = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _authTokenProviderMock
            .Setup(p => p.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(validToken);
        _authTokenProviderMock
            .Setup(p => p.MarkConsumedAsync(It.IsAny<Guid>()))
            .ReturnsAsync(validToken);
        _userProviderMock
            .Setup(p => p.GetOrCreateByEmailAsync(It.IsAny<string>(), null, null))
            .ReturnsAsync(user);

        await _sut.ValidateTokenAsync("some-token");

        _userProviderMock.Verify(p => p.UpdateEmailVerificationAsync(userId, true), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_SkipsEmailVerification_WhenAlreadyVerified()
    {
        var validToken = CreateValidAuthToken();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "test",
            EmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _authTokenProviderMock
            .Setup(p => p.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(validToken);
        _authTokenProviderMock
            .Setup(p => p.MarkConsumedAsync(It.IsAny<Guid>()))
            .ReturnsAsync(validToken);
        _userProviderMock
            .Setup(p => p.GetOrCreateByEmailAsync(It.IsAny<string>(), null, null))
            .ReturnsAsync(user);

        await _sut.ValidateTokenAsync("some-token");

        _userProviderMock.Verify(p => p.UpdateEmailVerificationAsync(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsUser_OnSuccess()
    {
        var validToken = CreateValidAuthToken();
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

        _authTokenProviderMock
            .Setup(p => p.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(validToken);
        _authTokenProviderMock
            .Setup(p => p.MarkConsumedAsync(It.IsAny<Guid>()))
            .ReturnsAsync(validToken);
        _userProviderMock
            .Setup(p => p.GetOrCreateByEmailAsync(It.IsAny<string>(), null, null))
            .ReturnsAsync(expectedUser);

        var result = await _sut.ValidateTokenAsync("some-token");

        result.Should().NotBeNull();
        result.Should().BeSameAs(expectedUser);
    }

    [Fact]
    public async Task CleanupExpiredTokensAsync_DelegatesToProvider()
    {
        _authTokenProviderMock
            .Setup(p => p.CleanupExpiredAsync())
            .ReturnsAsync(5);

        var result = await _sut.CleanupExpiredTokensAsync();

        result.Should().Be(5);
        _authTokenProviderMock.Verify(p => p.CleanupExpiredAsync(), Times.Once);
    }

    private static AuthenticationToken CreateValidAuthToken(Guid? id = null)
    {
        return new AuthenticationToken
        {
            Id = id ?? Guid.NewGuid(),
            Email = "test@example.com",
            TokenHash = "hash",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            ConsumedAt = null,
            IsRevoked = false
        };
    }

    private void SetupValidTokenScenario(AuthenticationToken token)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = token.Email,
            Username = "test",
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _authTokenProviderMock
            .Setup(p => p.GetByTokenHashAsync(It.IsAny<string>()))
            .ReturnsAsync(token);
        _authTokenProviderMock
            .Setup(p => p.MarkConsumedAsync(It.IsAny<Guid>()))
            .ReturnsAsync(token);
        _userProviderMock
            .Setup(p => p.GetOrCreateByEmailAsync(It.IsAny<string>(), null, null))
            .ReturnsAsync(user);
    }
}
