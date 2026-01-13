namespace Isdocovac.Services.Email;

public interface IEmailService
{
    /// <summary>
    /// Sends a magic link email to the specified email address
    /// </summary>
    /// <param name="email">The recipient email address</param>
    /// <param name="magicLinkUrl">The magic link URL</param>
    Task SendMagicLinkEmailAsync(string email, string magicLinkUrl);
}
