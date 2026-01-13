using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Isdocovac.Services.Authentication;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ISessionService _sessionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomAuthenticationStateProvider(
        ISessionService sessionService,
        IHttpContextAccessor httpContextAccessor)
    {
        _sessionService = sessionService;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Get the authenticated user from the cookie authentication
        var authenticatedUser = httpContext.User;
        if (authenticatedUser?.Identity?.IsAuthenticated != true)
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Extract session token from claims
        var sessionToken = authenticatedUser.FindFirst("SessionToken")?.Value;
        if (string.IsNullOrEmpty(sessionToken))
        {
            // If no session token in claims, user is authenticated but we can't validate the session
            return new AuthenticationState(authenticatedUser);
        }

        // Validate the session against our database
        var user = await _sessionService.ValidateSessionAsync(sessionToken);
        if (user == null)
        {
            // Session is invalid or expired, return unauthenticated state
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        // Session is valid, return the authenticated user
        return new AuthenticationState(authenticatedUser);
    }

    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
