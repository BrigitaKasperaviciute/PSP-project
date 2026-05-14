using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartDiscountControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public CartDiscountControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    public async Task InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateCartDiscount_WithValidPayload_ReturnsOkAndPersistsDiscount()
    {
        // Arrange
        var request = new CartDiscountRequestBuilder()
            .WithValue(10)
            .WithIsPercentage(true)
            .WithEndDate(DateTime.UtcNow.AddDays(30))
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(10);
        body.IsPercentage.Should().BeTrue();

        await using var db = _factory.CreateDbContext();
        var persisted = await db.CardDiscounts.AsNoTracking().SingleAsync(cartDiscount => cartDiscount.Id == body.Id);
        persisted.Value.Should().Be(10);
        persisted.IsPercentage.Should().BeTrue();
    }

    [Fact]
    public async Task GetCartDiscountById_WithExistingId_ReturnsOkAndDiscount()
    {
        // Arrange
        var createdDiscount = await CreateCartDiscountAsync();

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{createdDiscount.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdDiscount.Id);
    }

    [Fact]
    public async Task DeleteCartDiscountById_WithExistingId_ReturnsOkAndRemovesDiscount()
    {
        // Arrange
        var createdDiscount = await CreateCartDiscountAsync();

        // Act
        var response = await _client.DeleteAsync($"/api/cart-discount/{createdDiscount.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _factory.CreateDbContext();
        var deleted = await db.CardDiscounts.AsNoTracking().SingleOrDefaultAsync(cartDiscount => cartDiscount.Id == createdDiscount.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task GetCartDiscountById_WithMissingId_ReturnsNotFound()
    {
        // Arrange
        const string missingId = "NONEXISTENT-99999";

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{missingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<CartDiscountResponse> CreateCartDiscountAsync()
    {
        var request = new CartDiscountRequestBuilder()
            .WithValue(15)
            .WithIsPercentage(true)
            .WithEndDate(DateTime.UtcNow.AddDays(30))
            .Build();

        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        return body!;
    }
}
