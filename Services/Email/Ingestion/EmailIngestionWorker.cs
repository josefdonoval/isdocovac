using Isdocovac.Providers.Email;
using Microsoft.Extensions.Options;

namespace Isdocovac.Services.Email.Ingestion;

/// <summary>
/// Polls all enabled mailbox accounts on a fixed interval and pulls new messages onto the desk.
/// Note: only one app instance should run this — for HA, gate with a Postgres advisory lock.
/// </summary>
public class EmailIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EmailIngestionOptions _options;
    private readonly ILogger<EmailIngestionWorker> _logger;

    public EmailIngestionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<EmailIngestionOptions> options,
        ILogger<EmailIngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Email ingestion is disabled (EmailIngestion:Enabled=false). Worker will not poll.");
            return;
        }

        _logger.LogInformation(
            "Email ingestion worker started; polling every {Seconds}s.",
            _options.PollIntervalSeconds);

        var interval = TimeSpan.FromSeconds(Math.Max(15, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email ingestion worker iteration crashed; will retry next tick.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Email ingestion worker stopped.");
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var mailboxes = scope.ServiceProvider.GetRequiredService<IMailboxAccountProvider>();
        var ingestion = scope.ServiceProvider.GetRequiredService<IEmailIngestionService>();

        var due = await mailboxes.ListAllEnabledDueAsync(DateTime.UtcNow);
        foreach (var account in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var ingested = await ingestion.PullMailboxOnceAsync(account.Id, cancellationToken);
                if (ingested > 0)
                {
                    _logger.LogInformation(
                        "Mailbox {MailboxId} ({Username}): ingested {Count} new message(s).",
                        account.Id, account.Username, ingested);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Polling mailbox {MailboxId} failed.", account.Id);
            }
        }
    }
}
