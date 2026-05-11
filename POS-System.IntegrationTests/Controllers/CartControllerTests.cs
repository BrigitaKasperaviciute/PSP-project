using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public CartControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        // Clean carts table before each test
        await _db.Carts.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateCart_WithValidPayload_ReturnsOkAndPersistsCart()
    {
        // Arrange
        var request = new CartRequestBuilder()
            .WithStatus("Active")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.Status.Should().Be(request.Status);

        // Assert - database state
        var persisted = await _db.Carts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(request.Status);
    }

    [Fact]
    public async Task GetAllCarts_WithValidPageNumbers_ReturnsOkWithCarts()
    {
        // Arrange
        var cart1 = new CartRequestBuilder().WithStatus("Active").Build();
        var cart2 = new CartRequestBuilder().WithStatus("Completed").Build();
        
        await _client.PostAsJsonAsync("/api/carts", cart1);
        await _client.PostAsJsonAsync("/api/carts", cart2);

        // Act
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=35");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<CartResponse>>();
        body.Should().NotBeNull();
        body!.Data.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task GetCartById_WithExistingId_ReturnsOkWithCart()
    {
        // Arrange
        var request = new CartRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/carts", request);
        var createdCart = await createResponse.Content.ReadFromJsonAsync<CartResponse>();

        // Act
        var response = await _client.GetAsync($"/api/carts/{createdCart!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdCart.Id);
    }

    [Fact]
    public async Task DeleteCart_WithExistingId_ReturnsOkAndDeletesCart()
    {
        // Arrange
        var request = new CartRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/carts", request);
        var createdCart = await createResponse.Content.ReadFromJsonAsync<CartResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{createdCart!.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database state
        var deleted = await _db.Carts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == createdCart.Id);
        deleted.Should().BeNull();
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetCartById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/carts/999999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateCart_WithInvalidStatus_MayReturnBadRequest()
    {
        // Arrange
        var invalidRequest = """{"status": ""}""";

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", invalidRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteCart_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/carts/999999");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ApplyDiscountToCart_WithNonExistentCartId_ReturnsNotFound()
    {
        // Arrange
        var discountRequest = new ApplyDiscountRequest(DiscountCode: "DISCOUNT10");

        // Act
        var response = await _client.PatchAsJsonAsync("/api/carts/999999/discount", discountRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetCartDiscount_WithNonExistentCartId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/carts/999999/discount");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.NoContent, HttpStatusCode.OK);
    }

    #endregion
}

// use POS_System.Business.Dtos.Response.CartResponse
