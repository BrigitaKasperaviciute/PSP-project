using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Fixtures;
using POS_System.IntegrationTests.Helpers;

namespace POS_System.IntegrationTests.Tests;

[Collection("Integration")]
public class ServiceReservationControllerTests(PosWebApplicationFactory factory)
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

    private async Task<int> CreateCartItemAsync()
    {
        var cartResp = await _client.PostAsJsonAsync("/api/carts", new CartRequest { EmployeeVersionId = 1 });
        var cart = await cartResp.Content.ReadFromJsonAsync<IdResponse>();
        var itemResp = await _client.PostAsJsonAsync($"/api/carts/{cart!.Id}/items",
            new CartItemRequest { CartId = cart.Id, Quantity = 1, IsProduct = true, ProductVersionId = 4 });
        var item = await itemResp.Content.ReadFromJsonAsync<IdResponse>();
        return item!.Id;
    }

    private async Task<int> CreateTimeSlotAsync(int daysOffset)
    {
        var slotResp = await _client.PostAsJsonAsync("/api/time-slot",
            new TimeSlotRequest { EmployeeVersionId = 1, StartTime = DateTime.UtcNow.AddDays(daysOffset), IsAvailable = true });
        var slot = await slotResp.Content.ReadFromJsonAsync<IdResponse>();
        return slot!.Id;
    }

    private ServiceReservationRequest BuildRequest(int cartItemId, int timeSlotId) => new()
    {
        CartItemId = cartItemId,
        TimeSlotId = timeSlotId,
        BookingTime = DateTime.UtcNow.AddDays(1),
        CustomerName = "Test Customer",
        CustomerPhone = "1234567890",
        IsCancelled = false
    };

    // GET /api/service-reservation
    [Fact]
    public async Task GetAllServiceReservations_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/service-reservation");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAllServiceReservations_NoToken_ReturnsUnauthorized()
    {
        var response = await _anonClient.GetAsync("/api/service-reservation");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // POST /api/service-reservation
    [Fact]
    public async Task CreateServiceReservation_ValidRequest_ReturnsOk()
    {
        var cartItemId = await CreateCartItemAsync();
        var slotId = await CreateTimeSlotAsync(100);

        var response = await _client.PostAsJsonAsync("/api/service-reservation", BuildRequest(cartItemId, slotId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateServiceReservation_NullBody_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync<ServiceReservationRequest?>("/api/service-reservation", null);
        Assert.False(response.IsSuccessStatusCode);
    }

    // GET /api/service-reservation/{id}
    [Fact]
    public async Task GetServiceReservationById_ExistingId_ReturnsOk()
    {
        var cartItemId = await CreateCartItemAsync();
        var slotId = await CreateTimeSlotAsync(110);

        var created = await _client.PostAsJsonAsync("/api/service-reservation", BuildRequest(cartItemId, slotId));
        var reservation = await created.Content.ReadFromJsonAsync<IdResponse>();

        var response = await _client.GetAsync($"/api/service-reservation/{reservation!.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetServiceReservationById_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/service-reservation/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // PUT /api/service-reservation/{id}
    [Fact]
    public async Task UpdateServiceReservation_ExistingId_ReturnsOk()
    {
        var cartItemId = await CreateCartItemAsync();
        var slotId = await CreateTimeSlotAsync(120);

        var created = await _client.PostAsJsonAsync("/api/service-reservation", BuildRequest(cartItemId, slotId));
        var reservation = await created.Content.ReadFromJsonAsync<IdResponse>();

        var updateReq = BuildRequest(cartItemId, slotId) with { CustomerName = "Updated Customer" };
        var response = await _client.PutAsJsonAsync($"/api/service-reservation/{reservation!.Id}", updateReq);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateServiceReservation_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync("/api/service-reservation/99999",
            BuildRequest(1, 1));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record IdResponse(int Id);
}
