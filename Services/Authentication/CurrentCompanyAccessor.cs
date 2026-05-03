using System.Security.Claims;
using Isdocovac.Models;
using Isdocovac.Providers;
using Microsoft.AspNetCore.Components.Authorization;

namespace Isdocovac.Services.Authentication;

public interface ICurrentCompanyAccessor
{
    /// <summary>
    /// Returns the user's id from the current authentication state, or null if unauthenticated.
    /// </summary>
    Task<Guid?> GetCurrentUserIdAsync();

    /// <summary>
    /// Returns the active company for the current user, or null if none is set or it doesn't exist.
    /// Falls back to the user's first company and persists it as active when nothing is set yet.
    /// </summary>
    Task<Company?> GetActiveCompanyAsync();

    /// <summary>
    /// Convenience for code that just needs the id. Same fallback behaviour as GetActiveCompanyAsync.
    /// </summary>
    Task<Guid?> GetActiveCompanyIdAsync();

    /// <summary>
    /// Persists a new active company. Verifies the user owns the company before doing so.
    /// </summary>
    Task SetActiveCompanyAsync(Guid companyId);
}

public class CurrentCompanyAccessor : ICurrentCompanyAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISessionService _sessionService;
    private readonly ICompanyProvider _companyProvider;

    public CurrentCompanyAccessor(
        IHttpContextAccessor httpContextAccessor,
        ISessionService sessionService,
        ICompanyProvider companyProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _sessionService = sessionService;
        _companyProvider = companyProvider;
    }

    public Task<Guid?> GetCurrentUserIdAsync()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return Task.FromResult<Guid?>(null);
        var claim = user.FindFirst(ClaimTypes.NameIdentifier);
        if (claim != null && Guid.TryParse(claim.Value, out var id)) return Task.FromResult<Guid?>(id);
        return Task.FromResult<Guid?>(null);
    }

    public async Task<Company?> GetActiveCompanyAsync()
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null) return null;

        var sessionToken = GetSessionToken();
        Guid? activeId = sessionToken != null
            ? await _sessionService.GetActiveCompanyIdAsync(sessionToken)
            : null;

        if (activeId.HasValue)
        {
            var company = await _companyProvider.GetByIdAsync(activeId.Value);
            if (company != null && company.OwnerUserId == userId.Value && company.IsActive)
            {
                return company;
            }
        }

        // Fallback: pick the user's first company and persist it.
        var companies = await _companyProvider.ListByOwnerAsync(userId.Value);
        var first = companies.FirstOrDefault();
        if (first != null && sessionToken != null)
        {
            await _sessionService.SetActiveCompanyAsync(sessionToken, first.Id);
        }
        return first;
    }

    public async Task<Guid?> GetActiveCompanyIdAsync()
    {
        var c = await GetActiveCompanyAsync();
        return c?.Id;
    }

    public async Task SetActiveCompanyAsync(Guid companyId)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId == null) return;
        if (!await _companyProvider.UserOwnsAsync(userId.Value, companyId)) return;
        var sessionToken = GetSessionToken();
        if (sessionToken == null) return;
        await _sessionService.SetActiveCompanyAsync(sessionToken, companyId);
    }

    private string? GetSessionToken()
        => _httpContextAccessor.HttpContext?.User?.FindFirst("SessionToken")?.Value;
}
