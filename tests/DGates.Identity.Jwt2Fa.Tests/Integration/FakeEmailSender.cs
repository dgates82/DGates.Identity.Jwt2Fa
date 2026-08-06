using Microsoft.AspNetCore.Identity.UI.Services;
using System.Collections.Concurrent;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

/// <summary>Captures sent emails in memory instead of delivering them, for integration test assertions.</summary>
public class FakeEmailSender : IEmailSender
{
    public ConcurrentQueue<(string Email, string Subject, string HtmlMessage)> SentEmails { get; } = new();

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        SentEmails.Enqueue((email, subject, htmlMessage));
        return Task.CompletedTask;
    }
}
