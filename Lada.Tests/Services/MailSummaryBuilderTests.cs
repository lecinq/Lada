using System;
using System.Collections.Generic;
using Google.Apis.Gmail.v1.Data;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class MailSummaryBuilderTests
{
    private static Message BuildMessage(IEnumerable<string> labelIds, string from, string subject, string snippet, long internalDateMs)
    {
        return new Message
        {
            Id = "msg123",
            Snippet = snippet,
            LabelIds = new List<string>(labelIds),
            InternalDate = internalDateMs,
            Payload = new MessagePart
            {
                Headers = new List<MessagePartHeader>
                {
                    new() { Name = "From", Value = from },
                    new() { Name = "Subject", Value = subject }
                }
            }
        };
    }

    [Fact]
    public void FromMessage_WithUnreadLabel_SetsIsUnreadTrue()
    {
        var message = BuildMessage(new[] { "INBOX", "UNREAD" }, "a@b.com", "Hello", "snippet text", 1700000000000);

        var result = MailSummaryBuilder.FromMessage(message);

        Assert.True(result.IsUnread);
    }

    [Fact]
    public void FromMessage_WithoutUnreadLabel_SetsIsUnreadFalse()
    {
        var message = BuildMessage(new[] { "INBOX" }, "a@b.com", "Hello", "snippet text", 1700000000000);

        var result = MailSummaryBuilder.FromMessage(message);

        Assert.False(result.IsUnread);
    }

    [Fact]
    public void FromMessage_ExtractsFromAndSubjectHeaders()
    {
        var message = BuildMessage(new[] { "INBOX" }, "Alice <alice@example.com>", "Meeting tomorrow", "snippet", 1700000000000);

        var result = MailSummaryBuilder.FromMessage(message);

        Assert.Equal("Alice <alice@example.com>", result.From);
        Assert.Equal("Meeting tomorrow", result.Subject);
    }

    [Fact]
    public void FromMessage_MissingSubjectHeader_FallsBackToEmptyString()
    {
        var message = new Message
        {
            Id = "msg456",
            Snippet = "snippet",
            LabelIds = new List<string> { "INBOX" },
            InternalDate = 1700000000000,
            Payload = new MessagePart
            {
                Headers = new List<MessagePartHeader> { new() { Name = "From", Value = "a@b.com" } }
            }
        };

        var result = MailSummaryBuilder.FromMessage(message);

        Assert.Equal("", result.Subject);
    }

    [Fact]
    public void FromMessage_ConvertsInternalDateFromUnixMillisecondsUtc()
    {
        // 1700000000000 ms == 2023-11-14T22:13:20Z
        var message = BuildMessage(new[] { "INBOX" }, "a@b.com", "s", "snippet", 1700000000000);

        var result = MailSummaryBuilder.FromMessage(message);

        Assert.Equal(new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc), result.ReceivedUtc);
    }

    [Fact]
    public void FromMessage_CopiesIdAndSnippet()
    {
        var message = BuildMessage(new[] { "INBOX" }, "a@b.com", "s", "the snippet", 1700000000000);

        var result = MailSummaryBuilder.FromMessage(message);

        Assert.Equal("msg123", result.GmailMessageId);
        Assert.Equal("the snippet", result.Snippet);
    }
}
