using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class ServiceReservationControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ServiceReservationControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClientWithClaims("ServiceRead", "ServiceWrite", "CartItemRead", "CartItemWrite");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ServiceReservations.ExecuteDeleteAsync();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.TimeSlots.ExecuteDeleteAsync();
        await db.Carts.ExecuteUpdateAsync(s => s.SetProperty(c => c.CartDiscountId, (string?)null));
        await db.Carts.ExecuteDeleteAsync();
        await db.ServiceOnTaxes.ExecuteDeleteAsync();
        await db.ServiceOnItemDiscounts.ExecuteDeleteAsync();
        await db.Services.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int cartItemId, int timeSlotId)> CreateServiceCartItemAndTimeSlotAsync()
    {
        // Create a service
        var serviceResp = await _client.PostAsJsonAsync("/api/services",
            new ServiceBuilder().WithName("Haircut").WithPrice(2000).WithEmployeeId(1).Build());
        var service = await serviceResp.Content.ReadFromJsonAsync<ServiceResponse>();

        // Create a time slot
        var tsResp = await _client.PostAsJsonAsync("/api/time-slot",
            new TimeSlotBuilder().WithEmployeeVersionId(1).WithIsAvailable(true).Build());
        var timeSlot = await tsResp.Content.ReadFromJsonAsync<TimeSlotResponse>();

        // Create a cart
        var cartClient = _factory.CreateClient();
        var cartResp = await cartClient.PostAsJsonAsync("/api/carts", new CartBuilder().Build());
        var cart = await cartResp.Content.ReadFromJsonAsync<CartResponse>();

        // Create a cart item (service-based, not product)
        var cartItemResp = await _client.PostAsJsonAsync($"/api/carts/{cart!.Id}/items",
            new CartItemBuilder()
                .WithCartId(cart.Id)
                .WithServiceVersionId(service!.Id)
                .Build());
        var cartItem = await cartItemResp.Content.ReadFromJsonAsync<CartItemResponse>();

        return (cartItem!.Id, timeSlot!.Id);
    }

    private static ServiceReservationRequest BuildRequest(int cartItemId, int timeSlotId) => new()
    {
        CartItemId = cartItemId,
        TimeSlotId = timeSlotId,
        BookingTime = DateTime.UtcNow.AddDays(1),
        CustomerName = "Jane Test",
        CustomerPhone = "+37060000001",
        IsCancelled = false
    };

    // --- GetAll ---

    [Fact]
    public async Task GetAll_ReturnsOkAndPagedResponse()
    {
        var (cartItemId, timeSlotId) = await CreateServiceCartItemAndTimeSlotAsync();
        await _client.PostAsJsonAsync("/api/service-reservation", BuildRequest(cartItemId, timeSlotId));

        var response = await _client.GetAsync("/api/service-reservation?pageSize=10&pageNumber=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<ServiceReservationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/service-reservation");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkAndCorrectReservation()
    {
        var (cartItemId, timeSlotId) = await CreateServiceCartItemAndTimeSlotAsync();
        var created = await (await _client.PostAsJsonAsync("/api/service-reservation", BuildRequest(cartItemId, timeSlotId)))
            .Content.ReadFromJsonAsync<ServiceReservationResponse>();

        var response = await _client.GetAsync($"/api/service-reservation/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body!.Id.Should().Be(created.Id);
        body.CustomerName.Should().Be("Jane Test");
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/service-reservation/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersists()
    {
        var (cartItemId, timeSlotId) = await CreateServiceCartItemAndTimeSlotAsync();
        var request = BuildRequest(cartItemId, timeSlotId);

        var response = await _client.PostAsJsonAsync("/api/service-reservation", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body.Should().NotBeNull();
        body!.CartItemId.Should().Be(cartItemId);
        body.CustomerName.Should().Be("Jane Test");

        await using var db = _factory.CreateDbContext();
        var inDb = await db.ServiceReservations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == body.Id);
        inDb.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var readClient = _factory.CreateClientWithClaims("ServiceRead");
        var response = await readClient.PostAsJsonAsync("/api/service-reservation",
            new ServiceReservationRequest
            {
                CartItemId = 1,
                TimeSlotId = 1,
                BookingTime = DateTime.UtcNow,
                CustomerName = "X",
                CustomerPhone = "123",
                IsCancelled = false
            });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_ReturnsOkAndUpdatesFields()
    {
        var (cartItemId, timeSlotId) = await CreateServiceCartItemAndTimeSlotAsync();
        var created = await (await _client.PostAsJsonAsync("/api/service-reservation", BuildRequest(cartItemId, timeSlotId)))
            .Content.ReadFromJsonAsync<ServiceReservationResponse>();

        var updateRequest = BuildRequest(cartItemId, timeSlotId) with
        {
            CustomerName = "Updated Name",
            IsCancelled = true
        };
        var response = await _client.PutAsJsonAsync($"/api/service-reservation/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body!.CustomerName.Should().Be("Updated Name");
        body.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/service-reservation/999999",
            new ServiceReservationRequest
            {
                CartItemId = 1,
                TimeSlotId = 1,
                BookingTime = DateTime.UtcNow,
                CustomerName = "X",
                CustomerPhone = "123",
                IsCancelled = false
            });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
