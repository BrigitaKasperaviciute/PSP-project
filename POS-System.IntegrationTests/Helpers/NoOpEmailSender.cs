using POS_System.Business.Dtos;
using POS_System.Business.Utils;

namespace POS_System.IntegrationTests.Helpers;

public class NoOpEmailSender : IEmailSender
{
    public Task SendAsync(Message message) => Task.CompletedTask;
}
