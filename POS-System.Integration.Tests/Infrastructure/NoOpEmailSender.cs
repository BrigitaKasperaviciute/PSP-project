using POS_System.Business.Utils;

namespace POS_System.Integration.Tests.Infrastructure;

public class NoOpEmailSender : IEmailSender
{
    public Task SendAsync(Message message) => Task.CompletedTask;
}
