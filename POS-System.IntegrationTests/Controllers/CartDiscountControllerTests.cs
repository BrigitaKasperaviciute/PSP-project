using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

/// <summary>
/// CartDiscount.Create and .Delete both call the Stripe Coupon API at runtime,
/// so those endpoints are excluded from integration tests (they require live credentials).
/// GetById is tested using a CartDiscount pre-seeded directly into the database.
///
/// Pre-seeded (by test setup): Id="coupon_test_abc"  Value=20  IsPercentage=true
/// </summary>
public class CartDiscountControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string SeededCouponId = "coupon_test_abc";

    public CartDiscountControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateAnonymousClient(); // CartDiscount endpoints require no auth
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();

        // Pre-seed a CartDiscount directly — bypasses the Stripe dependency in the service layer
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.CardDiscounts.Add(new CartDiscount
        {
            Id           = SeededCouponId,
            Value        = 20,
            IsPercentage = true
        });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET /api/cart-discount/{id} ───────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithCartDiscount()
    {
        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{SeededCouponId}");
        var body = await response.Content.ReadAsStringAsync();
        var discount = JsonSerializer.Deserialize<CartDiscountResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        discount.Should().NotBeNull();
        discount!.Id.Should().Be(SeededCouponId);
        discount.Value.Should().Be(20);
        discount.IsPercentage.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/cart-discount/nonexistent_coupon_xyz");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
