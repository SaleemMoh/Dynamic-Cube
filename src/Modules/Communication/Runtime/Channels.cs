using DynamicCube.Modules.Communication.Domain;
using DynamicCube.Platform.Integration;
using DynamicCube.Platform.Security;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DynamicCube.Modules.Communication.Runtime;

public sealed class CommunicationOptions
{
    public const string SectionName = "Communication";

    public EmailOptions Email { get; set; } = new();

    public SmsOptions Sms { get; set; } = new();

    /// <summary>Party profile field with the email address notifications go to.</summary>
    public string PartyEmailField { get; set; } = "EMAIL";

    /// <summary>Party profile field with the mobile number SMS go to.</summary>
    public string PartyMobileField { get; set; } = "MOBILE";
}

/// <summary>SMTP server; the user name and password, when the server needs them, come from the secret store.</summary>
public sealed class EmailOptions
{
    /// <summary>Empty: email is not configured and email messages are recorded as skipped.</summary>
    public string? Host { get; set; }

    public int Port { get; set; } = 25;

    /// <summary>None, Auto (STARTTLS when offered), StartTls or SslOnConnect.</summary>
    public string Security { get; set; } = "Auto";

    public string From { get; set; } = "no-reply@localhost";

    public string? FromName { get; set; }

    /// <summary>Secret with <c>username</c> and <c>password</c> (<c>Secrets:{name}:*</c>); none for servers without sign-in.</summary>
    public string? SecretName { get; set; }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>SMS go through an Integration Manager operation of the SMS provider (registered sender, credentials and envelope there).</summary>
public sealed class SmsOptions
{
    /// <summary>Empty: SMS is not configured and SMS messages are recorded as skipped.</summary>
    public string? Operation { get; set; }

    public string MobileInput { get; set; } = "mobile";

    public string TextInput { get; set; } = "message";
}

/// <summary>Delivers a message over one channel; throws <see cref="MessageDeliveryException"/> when it cannot.</summary>
public interface IMessageChannel
{
    string Code { get; }

    /// <summary>Whether the deployment configured the channel; messages of an unconfigured channel are skipped.</summary>
    bool IsConfigured { get; }

    Task SendAsync(OutboundMessage message, CancellationToken cancellationToken);
}

public sealed class MessageDeliveryException(string message, Exception? inner = null) : Exception(message, inner);

internal sealed class EmailChannel(IOptions<CommunicationOptions> options, ISecretStore secrets) : IMessageChannel
{
    private readonly EmailOptions _options = options.Value.Email;

    public string Code => Changes.Channels.Email;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Host);

    public async Task SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        var mail = new MimeMessage();
        mail.From.Add(new MailboxAddress(_options.FromName ?? string.Empty, _options.From));
        mail.To.Add(new MailboxAddress(message.RecipientName ?? string.Empty, message.Address ?? throw new MessageDeliveryException("The message has no address.")));
        mail.Subject = message.Title ?? string.Empty;
        mail.Body = new BodyBuilder { HtmlBody = message.Body }.ToMessageBody();
        mail.Headers.Add("X-DynamicCube-Message", message.Id.ToString());

        using var client = new SmtpClient { Timeout = (int)_options.Timeout.TotalMilliseconds };
        try
        {
            var security = Enum.TryParse<SecureSocketOptions>(_options.Security, ignoreCase: true, out var parsed) ? parsed : SecureSocketOptions.Auto;
            await client.ConnectAsync(_options.Host!, _options.Port, security, cancellationToken);
            if (_options.SecretName is { } secret)
            {
                var user = secrets.Find(secret, "username") ?? throw new MessageDeliveryException($"Secret '{secret}' has no 'username' (configure Secrets:{secret}:username).");
                await client.AuthenticateAsync(user, secrets.Find(secret, "password") ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mail, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (Exception ex) when (ex is SmtpCommandException or SmtpProtocolException or AuthenticationException or IOException or System.Net.Sockets.SocketException or TimeoutException)
        {
            throw new MessageDeliveryException($"The email server did not take the message: {ex.Message}", ex);
        }
    }
}

internal sealed class SmsChannel(IOptions<CommunicationOptions> options, IIntegrationGateway gateway) : IMessageChannel
{
    private readonly SmsOptions _options = options.Value.Sms;

    public string Code => Changes.Channels.Sms;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Operation);

    public async Task SendAsync(OutboundMessage message, CancellationToken cancellationToken)
    {
        IntegrationResult result;
        try
        {
            result = await gateway.InvokeAsync(
                _options.Operation!,
                new Dictionary<string, string?> { [_options.MobileInput] = message.Address, [_options.TextInput] = message.Body },
                cancellationToken);
        }
        catch (Exception ex) when (ex is IntegrationOperationNotFoundException or IntegrationInputException)
        {
            throw new MessageDeliveryException($"The SMS operation '{_options.Operation}' cannot be called: {ex.Message}", ex);
        }

        if (!result.IsSuccess)
        {
            throw new MessageDeliveryException($"The SMS provider answered {result.Outcome} {result.Code}: {result.MessageEn ?? result.MessageAr} (call {result.CallId}).");
        }
    }
}
