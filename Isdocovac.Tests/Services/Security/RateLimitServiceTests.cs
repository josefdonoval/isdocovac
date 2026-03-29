using FluentAssertions;
using Isdocovac.Models;
using Isdocovac.Providers;
using Isdocovac.Services.Security;
using Isdocovac.Tests.Helpers;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Isdocovac.Tests.Services.Security;

public class RateLimitServiceTests
{
    private readonly Mock<ILoginAttemptProvider> _loginAttemptProviderMock;
    private readonly IConfiguration _defaultConfig;
    private readonly RateLimitService _sut;

    public RateLimitServiceTests()
    {
        _loginAttemptProviderMock = new Mock<ILoginAttemptProvider>();
        _defaultConfig = TestHelpers.BuildConfiguration(new Dictionary<string, string?>());
        _sut = new RateLimitService(_loginAttemptProviderMock.Object, _defaultConfig);
    }

    private RateLimitService CreateServiceWithConfig(Dictionary<string, string?> settings)
    {
        var config = TestHelpers.BuildConfiguration(settings);
        return new RateLimitService(_loginAttemptProviderMock.Object, config);
    }

    [Fact]
    public async Task IsEmailAllowedAsync_ReturnsTrue_WhenUnderLimit()
    {
        _loginAttemptProviderMock
            .Setup(p => p.GetRecentAttemptsByEmailAsync("test@example.com", It.IsAny<int>()))
            .ReturnsAsync(new List<LoginAttempt>
            {
                new() { Id = Guid.NewGuid(), Email = "test@example.com", AttemptedAt = DateTime.UtcNow }
            });

        var result = await _sut.IsEmailAllowedAsync("test@example.com");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmailAllowedAsync_ReturnsFalse_WhenAtLimit()
    {
        var attempts = Enumerable.Range(0, 3).Select(_ => new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            AttemptedAt = DateTime.UtcNow
        }).ToList();

        _loginAttemptProviderMock
            .Setup(p => p.GetRecentAttemptsByEmailAsync("test@example.com", It.IsAny<int>()))
            .ReturnsAsync(attempts);

        var result = await _sut.IsEmailAllowedAsync("test@example.com");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEmailAllowedAsync_UsesConfiguredLimit()
    {
        var service = CreateServiceWithConfig(new Dictionary<string, string?>
        {
            { "RateLimiting:MagicLink:PerEmailLimit", "5" }
        });

        var attempts = Enumerable.Range(0, 4).Select(_ => new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            AttemptedAt = DateTime.UtcNow
        }).ToList();

        _loginAttemptProviderMock
            .Setup(p => p.GetRecentAttemptsByEmailAsync("test@example.com", It.IsAny<int>()))
            .ReturnsAsync(attempts);

        var result = await service.IsEmailAllowedAsync("test@example.com");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsIpAddressAllowedAsync_ReturnsTrue_WhenUnderLimit()
    {
        _loginAttemptProviderMock
            .Setup(p => p.GetRecentAttemptsByIpAsync("127.0.0.1", It.IsAny<int>()))
            .ReturnsAsync(new List<LoginAttempt>());

        var result = await _sut.IsIpAddressAllowedAsync("127.0.0.1");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsIpAddressAllowedAsync_ReturnsFalse_WhenAtLimit()
    {
        var attempts = Enumerable.Range(0, 10).Select(_ => new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            IpAddress = "127.0.0.1",
            AttemptedAt = DateTime.UtcNow
        }).ToList();

        _loginAttemptProviderMock
            .Setup(p => p.GetRecentAttemptsByIpAsync("127.0.0.1", It.IsAny<int>()))
            .ReturnsAsync(attempts);

        var result = await _sut.IsIpAddressAllowedAsync("127.0.0.1");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsIpAddressAllowedAsync_UsesConfiguredLimit()
    {
        var service = CreateServiceWithConfig(new Dictionary<string, string?>
        {
            { "RateLimiting:MagicLink:PerIpLimit", "2" }
        });

        var attempts = Enumerable.Range(0, 2).Select(_ => new LoginAttempt
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            IpAddress = "127.0.0.1",
            AttemptedAt = DateTime.UtcNow
        }).ToList();

        _loginAttemptProviderMock
            .Setup(p => p.GetRecentAttemptsByIpAsync("127.0.0.1", It.IsAny<int>()))
            .ReturnsAsync(attempts);

        var result = await service.IsIpAddressAllowedAsync("127.0.0.1");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RecordAttemptAsync_DelegatesToProvider()
    {
        await _sut.RecordAttemptAsync("test@example.com", "127.0.0.1", true, null);

        _loginAttemptProviderMock.Verify(p => p.RecordAttemptAsync(
            "test@example.com", "127.0.0.1", true, null), Times.Once);
    }

    [Fact]
    public async Task UsesDefaultValues_WhenConfigNotSet()
    {
        _loginAttemptProviderMock
            .Setup(p => p.GetRecentAttemptsByEmailAsync(It.IsAny<string>(), 15))
            .ReturnsAsync(new List<LoginAttempt>());

        await _sut.IsEmailAllowedAsync("test@example.com");

        _loginAttemptProviderMock.Verify(p => p.GetRecentAttemptsByEmailAsync("test@example.com", 15), Times.Once);
    }
}
