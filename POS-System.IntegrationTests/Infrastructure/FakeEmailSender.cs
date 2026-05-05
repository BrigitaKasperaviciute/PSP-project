using POS_System.Business.Utils;

namespace POS_System.IntegrationTests.Infrastructure;

internal sealed class FakeEmailSender : IEmailSender
{
    public Task SendAsync(Message message) => Task.CompletedTask;
}
