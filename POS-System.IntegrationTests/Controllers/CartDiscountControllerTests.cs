using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartDiscountControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    // CartDiscountController has no [Authorize] – use plain client.
    private readonly HttpClient _client;

    public CartDiscountControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        // Unlink any carts referencing discounts before deleting
        await db.Carts.ExecuteUpdateAsync(s => s.SetProperty(c => c.CartDiscountId, (string?)null));
        await db.CardDiscounts.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsDiscount()
    {
        // Arrange
        var request = new CartDiscountBuilder().WithValue(20).WithIsPercentage(true).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(20);
        body.IsPercentage.Should().BeTrue();
        body.Id.Should().NotBeNullOrEmpty();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.CardDiscounts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(20);
    }

    [Fact]
    public async Task Create_WithZeroValue_StillPersists()
    {
        // Arrange – zero-value discount is technically valid (no validation blocking it)
        var request = new CartDiscountBuilder().WithValue(0).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectDiscount()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/cart-discount", new CartDiscountBuilder().WithValue(30).Build()))
            .Content.ReadFromJsonAsync<CartDiscountResponse>();

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.Value.Should().Be(30);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/cart-discount/nonexistent-discount-id");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndRemovesDiscount()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/cart-discount", new CartDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDiscountResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/cart-discount/{created!.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database: discount removed
        await using var db = _factory.CreateDbContext();
        var inDb = await db.CardDiscounts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/cart-discount/nonexistent-id-9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
