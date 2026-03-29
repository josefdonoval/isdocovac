using System.Security.Claims;
using FluentAssertions;
using Isdocovac.Models;
using Isdocovac.Services.Authentication;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Isdocovac.Tests.Services.Authentication;

public class CustomAuthenticationStateProviderTests
{
    private readonly Mock<ISessionService> _sessionServiceMock;
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
    private readonly CustomAuthenticationStateProvider _sut;

    public CustomAuthenticationStateProviderTests()
    {
        _sessionServiceMock = new Mock<ISessionService>();
        _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        _sut = new CustomAuthenticationStateProvider(_sessionServiceMock.Object, _httpContextAccessorMock.Object);
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsUnauthenticated_WhenHttpContextIsNull()
    {
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var state = await _sut.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsUnauthenticated_WhenUserNotAuthenticated()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(context);

        var state = await _sut.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsExistingUser_WhenNoSessionTokenClaim()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser")
        }, "TestAuth");
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(identity);
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(context);

        var state = await _sut.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeTrue();
        state.User.Identity!.Name.Should().Be("testuser");
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsUnauthenticated_WhenSessionValidationFails()
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("SessionToken", "invalid-session-token")
        }, "TestAuth");
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(identity);
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(context);
        _sessionServiceMock
            .Setup(s => s.ValidateSessionAsync("invalid-session-token"))
            .ReturnsAsync((User?)null);

        var state = await _sut.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_ReturnsAuthenticatedUser_WhenSessionValid()
    {
        var sessionToken = "valid-session-token";
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("SessionToken", sessionToken)
        }, "TestAuth");
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(identity);
        _httpContextAccessorMock.Setup(a => a.HttpContext).Returns(context);

        var validUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Username = "testuser",
            EmailVerified = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _sessionServiceMock
            .Setup(s => s.ValidateSessionAsync(sessionToken))
            .ReturnsAsync(validUser);

        var state = await _sut.GetAuthenticationStateAsync();

        state.User.Identity!.IsAuthenticated.Should().BeTrue();
    }
}
