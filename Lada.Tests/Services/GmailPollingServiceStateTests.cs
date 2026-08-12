using System.Collections.Generic;
using Lada.Models;
using Lada.Services;
using Xunit;

namespace Lada.Tests.Services;

public class GmailPollingServiceStateTests
{
    [Fact]
    public void ApplyFetchResult_Success_ReplacesLastKnownMails()
    {
        var service = new GmailPollingService();
        var mails = new List<MailSummary> { new() { Subject = "Hi" } };

        service.ApplyFetchResult(success: true, mails);

        Assert.Single(service.LastKnownMails);
        Assert.Equal("Hi", service.LastKnownMails[0].Subject);
    }

    [Fact]
    public void ApplyFetchResult_FailureAfterSuccess_KeepsPreviousList()
    {
        var service = new GmailPollingService();
        var mails = new List<MailSummary> { new() { Subject = "Hi" } };
        service.ApplyFetchResult(success: true, mails);

        service.ApplyFetchResult(success: false, new List<MailSummary>());

        Assert.Single(service.LastKnownMails);
        Assert.Equal("Hi", service.LastKnownMails[0].Subject);
    }

    [Fact]
    public void ApplyFetchResult_Success_RaisesMailsUpdated()
    {
        var service = new GmailPollingService();
        var raised = false;
        service.MailsUpdated += () => raised = true;

        service.ApplyFetchResult(success: true, new List<MailSummary>());

        Assert.True(raised);
    }

    [Fact]
    public void ApplyFetchResult_Failure_DoesNotRaiseMailsUpdated()
    {
        var service = new GmailPollingService();
        var raised = false;
        service.MailsUpdated += () => raised = true;

        service.ApplyFetchResult(success: false, new List<MailSummary>());

        Assert.False(raised);
    }
}
