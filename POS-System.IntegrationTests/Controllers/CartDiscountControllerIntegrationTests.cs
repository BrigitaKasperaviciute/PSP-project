using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class CartDiscountControllerCoverageTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _client = null!;

    public CartDiscountControllerCoverageTests(ApiTestFactory factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetCartDiscountById_SeededDiscount_ReturnsOkAndMatchesDatabase()
    {
        // Arrange
        var cartDiscount = await SeedCartDiscountAsync();

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{cartDiscount.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(cartDiscount.Id);
        body.Value.Should().Be(cartDiscount.Value);
        body.IsPercentage.Should().Be(cartDiscount.IsPercentage);
    }

    [Fact]
    public async Task GetCartDiscountById_MissingDiscount_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/cart-discount/missing-cart-discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCartDiscount_MissingDiscount_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/cart-discount/missing-cart-discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<CartDiscount> SeedCartDiscountAsync()
    {
        await using var db = _factory.GetDbContext();

        var cartDiscount = new CartDiscount
        {
            Id = $"cart-discount-{Guid.NewGuid():N}",
            Value = 15,
            IsPercentage = true
        };

        db.Set<CartDiscount>().Add(cartDiscount);
        await db.SaveChangesAsync();

        return cartDiscount;
    }
}