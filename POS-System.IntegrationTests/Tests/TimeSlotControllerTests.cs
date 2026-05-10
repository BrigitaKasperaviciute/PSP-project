using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class TimeSlotControllerTests(PosWebApplicationFactory factory)
{
    private readonly HttpClient _client = CreateAuthClient(factory);
    private readonly HttpClient _anonClient = factory.CreateClient();

    private static HttpClient CreateAuthClient(PosWebApplicationFactory f)
    {
        var c = f.CreateClient();
        c.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTokenHelper.FullAccessToken);
        return c;
    }

    // GET /api/time-slot
    [Fact]
    public async Task GetAllTimeSlots_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/time-slot");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllTimeSlots_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/time-slot");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // GET /api/time-slot/{id}
    [Fact]
    public async Task GetTimeSlotById_ExistingId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/time-slot/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetTimeSlotById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/time-slot/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // POST /api/time-slot
    [Fact]
    public async Task CreateTimeSlot_ValidRequest_ReturnsOk()
    {
        var request = new TimeSlotRequest
        {
            EmployeeVersionId = 1,
            StartTime = DateTime.UtcNow.AddDays(1),
            IsAvailable = true
        };
        var response = await _client.PostAsJsonAsync("/api/time-slot", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateTimeSlot_NoToken_ReturnsUnauthorized()
    {
        var request = new TimeSlotRequest { EmployeeVersionId = 1, StartTime = DateTime.UtcNow, IsAvailable = true };
        var response = await _anonClient.PostAsJsonAsync("/api/time-slot", request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // PUT /api/time-slot/{id}
    [Fact]
    public async Task UpdateTimeSlot_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/time-slot",
            new TimeSlotRequest { EmployeeVersionId = 1, StartTime = DateTime.UtcNow.AddDays(2), IsAvailable = true });
        var slot = await created.Content.ReadFromJsonAsync<IdResponse>();

        var updateReq = new TimeSlotRequest { EmployeeVersionId = 1, StartTime = DateTime.UtcNow.AddDays(3), IsAvailable = false };
        var response = await _client.PutAsJsonAsync($"/api/time-slot/{slot!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTimeSlot_NonExistentId_ReturnsNotFound()
    {
        var updateReq = new TimeSlotRequest { EmployeeVersionId = 1, StartTime = DateTime.UtcNow.AddDays(1), IsAvailable = true };
        var response = await _client.PutAsJsonAsync("/api/time-slot/99999", updateReq);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // DELETE /api/time-slot/{id}
    [Fact]
    public async Task DeleteTimeSlot_ExistingId_ReturnsOk()
    {
        var created = await _client.PostAsJsonAsync("/api/time-slot",
            new TimeSlotRequest { EmployeeVersionId = 1, StartTime = DateTime.UtcNow.AddDays(4), IsAvailable = true });
        var slot = await created.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.DeleteAsync($"/api/time-slot/{slot!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTimeSlot_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/time-slot/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record IdResponse(int Id);
}
