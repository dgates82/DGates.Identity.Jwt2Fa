using Microsoft.AspNetCore.Identity.UI.Services;
using System.Collections.Concurrent;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

/// <summary>Captures sent emails in memory instead of delivering them, for integration test assertions.</summary>
public class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<(string Email, string Subject, string HtmlMessage)> SentEmails { get; } = new();

    /// <summary>When set, the next <see cref="SendEmailAsync"/> call throws instead of capturing the email — for
    /// tests forcing a real unhandled exception through the pipeline to verify it's caught and not leaked.</summary>
    public bool ThrowOnNextSend { get; set; }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        if (ThrowOnNextSend)
        {
            ThrowOnNextSend = false;
            throw new InvalidOperationException("Simulated email delivery failure for testing.");
        }

        SentEmails.Enqueue((email, subject, htmlMessage));
        return Task.CompletedTask;
    }
}
