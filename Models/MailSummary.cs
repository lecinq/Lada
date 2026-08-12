using System;

namespace Lada.Models;

public sealed class MailSummary
{
    public string From { get; init; } = "";
    public string Subject { get; init; } = "";
    public string Snippet { get; init; } = "";
    public DateTime ReceivedUtc { get; init; }
    public bool IsUnread { get; init; }
    public string GmailMessageId { get; init; } = "";
}
