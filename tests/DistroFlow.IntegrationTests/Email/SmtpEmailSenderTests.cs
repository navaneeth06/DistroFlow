using System.Net.Http.Json;
using System.Text.Json;
using DistroFlow.Application.Interfaces.Email;
using DistroFlow.Infrastructure.Email;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Options;

namespace DistroFlow.IntegrationTests.Email;

/// <summary>
/// Sends real SMTP mail to a Mailpit container and reads it back through Mailpit's API.
/// </summary>
public class SmtpEmailSenderTests : IAsyncLifetime
{
    private const int SmtpPort = 1025;
    private const int ApiPort = 8025;

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.33.1")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(ApiPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(request => request.ForPort(ApiPort).ForPath("/api/v1/info")))
        .Build();

    public Task InitializeAsync() => _mailpit.StartAsync();

    public Task DisposeAsync() => _mailpit.DisposeAsync().AsTask();

    [Fact]
    public async Task Sends_html_and_text_email_to_the_smtp_server()
    {
        var sender = CreateSender(useTls: false);

        await sender.SendAsync(new EmailMessage(
            To: "anita@example.com",
            Subject: "You're invited to DistroFlow",
            HtmlBody: "<p>Click <a href=\"https://example.com/set\">here</a></p>",
            TextBody: "Open https://example.com/set"));

        using var api = new HttpClient { BaseAddress = new Uri($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(ApiPort)}") };

        var list = await api.GetFromJsonAsync<JsonElement>("/api/v1/messages");
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        var summary = list.GetProperty("messages")[0];
        Assert.Equal("You're invited to DistroFlow", summary.GetProperty("Subject").GetString());
        Assert.Equal("anita@example.com", summary.GetProperty("To")[0].GetProperty("Address").GetString());
        Assert.Equal("no-reply@distroflow.test", summary.GetProperty("From").GetProperty("Address").GetString());
        Assert.Equal("DistroFlow Tests", summary.GetProperty("From").GetProperty("Name").GetString());

        var message = await api.GetFromJsonAsync<JsonElement>($"/api/v1/message/{summary.GetProperty("ID").GetString()}");
        Assert.Contains("https://example.com/set", message.GetProperty("HTML").GetString());
        Assert.Contains("https://example.com/set", message.GetProperty("Text").GetString());
    }

    [Fact]
    public async Task With_tls_enabled_it_refuses_a_server_without_tls_instead_of_sending_in_plain_text()
    {
        // Mailpit offers no STARTTLS. With UseTls = true, sending must fail rather than
        // silently fall back to an unencrypted connection.
        var sender = CreateSender(useTls: true);

        await Assert.ThrowsAnyAsync<Exception>(() => sender.SendAsync(
            new EmailMessage("anita@example.com", "Subject", "<p>Body</p>", "Body")));
    }

    private SmtpEmailSender CreateSender(bool useTls) =>
        new(Options.Create(new SmtpOptions
        {
            Host = _mailpit.Hostname,
            Port = _mailpit.GetMappedPublicPort(SmtpPort),
            UseTls = useTls,
            FromAddress = "no-reply@distroflow.test",
            FromName = "DistroFlow Tests"
        }));
}
