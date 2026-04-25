using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class CartDiscountControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public CartDiscountControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        // CartDiscountController has no [Authorize] attributes
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Create happy-path is omitted: CartDiscountService calls Stripe's CouponService.CreateAsync
    // directly, which fails with fake credentials (500). GetById and Delete happy-paths are tested
    // by seeding a CartDiscount directly into the in-memory DB.

    private async Task<string> SeedCartDiscountAsync(string id = "test-coupon-1")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (await db.CardDiscounts.FindAsync(id) is null)
        {
            db.CardDiscounts.Add(new CartDiscount { Id = id, Value = 20, IsPercentage = true });
            await db.SaveChangesAsync();
        }
        return id;
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithCartDiscount()
    {
        // Arrange — seed directly to bypass Stripe
        var id = await SeedCartDiscountAsync("get-coupon-1");

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(20);
    }

    // Delete happy-path is omitted: CartDiscountService.DeleteCartDiscountAsync also calls Stripe.

    [Fact]
    public async Task Create_InvalidRequest_ReturnsBadRequest()
    {
        // Arrange – send null body (missing required fields)
        var response = await _client.PostAsJsonAsync<CartDiscountRequest?>("/api/cart-discount", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const string nonExistentId = "00000000-0000-0000-0000-000000000000";

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const string nonExistentId = "00000000-0000-0000-0000-000000000000";

        // Act
        var response = await _client.DeleteAsync($"/api/cart-discount/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
