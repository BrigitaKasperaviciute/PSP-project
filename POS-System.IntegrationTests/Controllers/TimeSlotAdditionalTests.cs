using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.IntegrationTests.TestSupport;
using POS_System.IntegrationTests.Helpers;
using POS_System.Business.Dtos.Request;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class TimeSlotAdditionalTests
{
    [Fact]
    public async Task Create_GetById_Delete_TimeSlot()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        // allow both read and write for simplicity
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ServiceWrite,ServiceRead");

        var request = new TimeSlotRequest { EmployeeVersionId = 0, StartTime = DateTime.UtcNow.AddHours(2), IsAvailable = true };

        var createResp = await client.PostAsync("api/time-slot", TestDataFactory.ToJsonContent(request));
        createResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // get id
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var slot = db.TimeSlots.SingleOrDefault(s => s.EmployeeVersionId == 0 && s.IsAvailable == true);
        slot.Should().NotBeNull();
        var id = slot!.Id;

        // GET by id
        var getResp = await client.GetAsync($"api/time-slot/{id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // DELETE
        var delResp = await client.DeleteAsync($"api/time-slot/{id}");
        delResp.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope2 = factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var updated = db2.TimeSlots.SingleOrDefault(t => t.Id == id);
        updated.Should().NotBeNull();
        updated!.IsAvailable.Should().BeFalse();
    }
}
