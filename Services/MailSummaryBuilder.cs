using System;
using System.Linq;
using Google.Apis.Gmail.v1.Data;
using Lada.Models;

namespace Lada.Services;

// Pure transform from the SDK's raw Message shape to the flat MailSummary
// the UI renders -- kept separate from GmailPollingService so it's testable
// without any network/auth setup, matching TimerCountdownCalculator's role
// for the Timer widget.
public static class MailSummaryBuilder
{
    public static MailSummary FromMessage(Message message)
    {
        var headers = message.Payload?.Headers;
        var from = HeaderValue(headers, "From");
        var subject = HeaderValue(headers, "Subject");

        return new MailSummary
        {
            From = from,
            Subject = subject,
            Snippet = message.Snippet ?? "",
            ReceivedUtc = DateTimeOffset.FromUnixTimeMilliseconds(message.InternalDate ?? 0).UtcDateTime,
            IsUnread = message.LabelIds?.Contains("UNREAD") ?? false,
            GmailMessageId = message.Id ?? ""
        };
    }

    private static string HeaderValue(System.Collections.Generic.IList<MessagePartHeader>? headers, string name) =>
        headers?.FirstOrDefault(h => string.Equals(h.Name, name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
}
