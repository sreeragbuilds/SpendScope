using Microsoft.Extensions.Logging;
using SpendScope.Application.Abstractions;

namespace SpendScope.Infrastructure.Services;

/// <summary>
/// Development email sender: writes the message to the log instead of sending it. The body can
/// contain a password reset token, so it is registered only in Development and Testing.
/// </summary>
internal sealed class ConsoleEmailSender(ILogger<ConsoleEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("DEV EMAIL to {To}: {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

/// <summary>Used outside Development until a real provider exists. Never logs the body.</summary>
internal sealed class DisabledEmailSender(ILogger<DisabledEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogWarning("Email not sent: no email provider is configured (subject: {Subject})", subject);
        return Task.CompletedTask;
    }
}
