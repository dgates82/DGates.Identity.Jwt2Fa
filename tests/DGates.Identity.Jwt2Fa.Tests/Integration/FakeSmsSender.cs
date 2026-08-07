using DGates.Identity.NotificationProviders.Abstractions;
using System.Collections.Concurrent;

namespace DGates.Identity.Jwt2Fa.Tests.Integration;

/// <summary>Captures sent SMS messages in memory instead of delivering them, for integration test assertions.</summary>
public class FakeSmsSender : ISmsSender
{
    public ConcurrentQueue<(string Number, string Message)> SentMessages { get; } = new();

    public Task<string> SendSmsAsync(string number, string message, CancellationToken cancellationToken = default)
    {
        SentMessages.Enqueue((number, message));
        return Task.FromResult("fake-message-id");
    }
}
