using POS_System.Business.Utils;

namespace POS_System.Integration.Tests.Helpers;

public class FakeEmailSender : IEmailSender
{
    public Task SendAsync(Message message) => Task.CompletedTask;
}
