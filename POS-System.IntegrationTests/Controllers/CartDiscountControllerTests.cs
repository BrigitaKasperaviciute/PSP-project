using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartDiscountControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CartDiscountControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.CardDiscounts.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- CreateCartDiscount ---------------

    [Fact]
    public async Task CreateCartDiscount_WithValidRequest_ReturnsOkAndPersists()
    {
        // Arrange
        var request = new CartDiscountRequest
        {
            Value = 20,
            IsPercentage = true,
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeNullOrEmpty();
        body.Value.Should().Be(request.Value);
        body.IsPercentage.Should().Be(request.IsPercentage);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.CardDiscounts.AsNoTracking()
            .SingleOrDefaultAsync(cd => cd.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Value.Should().Be(request.Value);
    }

    [Fact]
    public async Task CreateCartDiscount_WithZeroValue_ReturnsOk()
    {
        // Arrange – CartDiscountRequest has no validator rejecting zero values
        var request = new CartDiscountRequest
        {
            Value = 0,
            IsPercentage = true,
            EndDate = DateTime.UtcNow.AddDays(30)
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        // Assert – no FluentValidation rule blocks zero; Stripe mock accepts it
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --------------- GetCartDiscountById ---------------

    [Fact]
    public async Task GetCartDiscountById_WhenDiscountExists_ReturnsOkWithDiscount()
    {
        // Arrange
        var created = await CreateCartDiscountAsync(15, false);

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{created.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.Value.Should().Be(15);
        body.IsPercentage.Should().BeFalse();
    }

    [Fact]
    public async Task GetCartDiscountById_WhenDiscountDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string nonExistentId = "non-existent-discount-id";

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- DeleteCartDiscountById ---------------

    [Fact]
    public async Task DeleteCartDiscountById_WhenDiscountExists_ReturnsOk()
    {
        // Arrange
        var created = await CreateCartDiscountAsync(10, true);

        // Act
        var response = await _client.DeleteAsync($"/api/cart-discount/{created.Id}");

        // Assert – CartDiscountService.DeleteCartDiscountAsync deletes from Stripe only, not from DB
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteCartDiscountById_WhenDiscountDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const string nonExistentId = "does-not-exist-99999";

        // Act
        var response = await _client.DeleteAsync($"/api/cart-discount/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ---- helpers ----

    private async Task<CartDiscountResponse> CreateCartDiscountAsync(int value, bool isPercentage)
    {
        var response = await _client.PostAsJsonAsync("/api/cart-discount",
            new CartDiscountRequest
            {
                Value = value,
                IsPercentage = isPercentage,
                EndDate = DateTime.UtcNow.AddDays(7)
            });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CartDiscountResponse>())!;
    }
}
