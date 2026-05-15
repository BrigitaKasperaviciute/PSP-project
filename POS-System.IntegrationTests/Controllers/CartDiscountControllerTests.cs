using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

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
        // Cart-discount endpoints have no [Authorize]
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        // Note: DbSet is named CardDiscounts (typo in production code)
        await db.CardDiscounts.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkWithStringId()
    {
        // Arrange
        var request = new CartDiscountBuilder().WithValue(15).WithIsPercentage(true).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().NotBeNullOrEmpty();
        body.Value.Should().Be(15);
        body.IsPercentage.Should().BeTrue();
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCartDiscount()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/cart-discount",
                new CartDiscountBuilder().WithValue(20).Build()))
            .Content.ReadFromJsonAsync<CartDiscountResponse>();

        // Act
        var response = await _client.GetAsync($"/api/cart-discount/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body!.Id.Should().Be(created.Id);
        body.Value.Should().Be(20);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/cart-discount/nonexistent-id-xyz");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOk()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/cart-discount",
                new CartDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDiscountResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/cart-discount/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/cart-discount/nonexistent-id-abc");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
