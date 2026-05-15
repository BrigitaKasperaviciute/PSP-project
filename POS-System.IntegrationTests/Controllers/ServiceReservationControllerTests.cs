using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

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
        await db.CartItems.Where(ci => ci.ServiceVersionId != null).ExecuteDeleteAsync();
        await db.TimeSlots.ExecuteDeleteAsync();
        await db.Carts.ExecuteDeleteAsync();
        await db.ServiceOnTaxes.ExecuteDeleteAsync();
        await db.ServiceOnItemDiscounts.ExecuteDeleteAsync();
        await db.EmployeeOnServices.ExecuteDeleteAsync();
        await db.Services.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(CartItemResponse cartItem, TimeSlotResponse timeSlot)> SetupReservationPrerequisitesAsync()
    {
        // Create a service
        var service = await (await _client.PostAsJsonAsync("/api/services",
                new ServiceBuilder().WithEmployeeId(1).Build()))
            .Content.ReadFromJsonAsync<ServiceResponse>();

        // Create a cart
        var cartClient = _factory.CreateClient();
        var cart = await (await cartClient.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDto>();

        // Create a cart item linked to the service
        var cartItem = await (await _client.PostAsJsonAsync($"/api/carts/{cart!.Id}/items",
                new CartItemRequest
                {
                    CartId = cart.Id,
                    Quantity = 1,
                    IsProduct = false,
                    ServiceVersionId = service!.Id
                }))
            .Content.ReadFromJsonAsync<CartItemResponse>();

        // Create a time slot
        var timeSlot = await (await _client.PostAsJsonAsync("/api/time-slot",
                new TimeSlotBuilder().WithEmployeeVersionId(1).Build()))
            .Content.ReadFromJsonAsync<TimeSlotResponse>();

        return (cartItem!, timeSlot!);
    }

    // --- GetAll ---


    [Fact]
    public async Task GetAll_WithServiceReadClaim_ReturnsOk()
    {
        // Arrange
        var (cartItem, timeSlot) = await SetupReservationPrerequisitesAsync();
        await _client.PostAsJsonAsync("/api/service-reservation",
            new ServiceReservationBuilder().WithCartItemId(cartItem.Id).WithTimeSlotId(timeSlot.Id).Build());

        // Act
        var response = await _client.GetAsync("/api/service-reservation");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedServiceReservationResponse<ServiceReservationResponse>>();
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
    public async Task GetById_WithExistingId_ReturnsReservation()
    {
        // Arrange
        var (cartItem, timeSlot) = await SetupReservationPrerequisitesAsync();
        var created = await (await _client.PostAsJsonAsync("/api/service-reservation",
                new ServiceReservationBuilder().WithCartItemId(cartItem.Id).WithTimeSlotId(timeSlot.Id)
                    .WithCustomerNameAndPhone("Alice", "37061234567").Build()))
            .Content.ReadFromJsonAsync<ServiceReservationResponse>();

        // Act
        var response = await _client.GetAsync($"/api/service-reservation/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body!.CustomerName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/service-reservation/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersists()
    {
        // Arrange
        var (cartItem, timeSlot) = await SetupReservationPrerequisitesAsync();
        var request = new ServiceReservationBuilder()
            .WithCartItemId(cartItem.Id)
            .WithTimeSlotId(timeSlot.Id)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/service-reservation", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body!.CartItemId.Should().Be(cartItem.Id);
        body.IsCancelled.Should().BeFalse();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.ServiceReservations.AsNoTracking().SingleOrDefaultAsync(r => r.Id == body.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var readOnlyClient = _factory.CreateClientWithClaims("ServiceRead");
        var response = await readOnlyClient.PostAsJsonAsync("/api/service-reservation",
            new ServiceReservationBuilder().WithCartItemId(1).Build());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_CancellingReservation_ReturnsCancelledReservation()
    {
        // Arrange
        var (cartItem, timeSlot) = await SetupReservationPrerequisitesAsync();
        var created = await (await _client.PostAsJsonAsync("/api/service-reservation",
                new ServiceReservationBuilder().WithCartItemId(cartItem.Id).WithTimeSlotId(timeSlot.Id).Build()))
            .Content.ReadFromJsonAsync<ServiceReservationResponse>();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/service-reservation/{created!.Id}",
            new ServiceReservationBuilder().WithCartItemId(cartItem.Id).WithTimeSlotId(timeSlot.Id)
                .WithIsCancelled(true).Build());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ServiceReservationResponse>();
        body!.IsCancelled.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/service-reservation/999999",
            new ServiceReservationBuilder().WithCartItemId(1).Build());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

file record CartDto(int Id, int EmployeeVersionId);
file record PagedServiceReservationResponse<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
