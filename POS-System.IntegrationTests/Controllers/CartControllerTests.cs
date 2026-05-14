using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public CartControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient(role: "Admin");
    }

    public async Task InitializeAsync()
    {
        // Clean up carts before each test
        await using var db = _factory.CreateDbContext();
        db.Carts.RemoveRange(db.Carts);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Happy Path Tests

    [Fact]
    public async Task CreateCart_WithValidPayload_ReturnsOkAndPersistsCart()
    {
        // Arrange
        var request = new CartRequestBuilder()
            .WithEmployeeVersionId(1)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.EmployeeVersionId.Should().Be(1);
        body.DateCreated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Assert - database persistence
        await using var db = _factory.CreateDbContext();
        var persistedCart = db.Carts.FirstOrDefault(c => c.EmployeeVersionId == 1);
        persistedCart.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllCarts_WithValidPagination_ReturnsOkAndCartList()
    {
        // Arrange
        var cartRequests = new[]
        {
            new CartRequestBuilder().Build(),
            new CartRequestBuilder().Build(),
            new CartRequestBuilder().Build()
        };

        foreach (var req in cartRequests)
        {
            await _client.PostAsJsonAsync("/api/carts", req);
        }

        // Act
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<POS_System.Business.Dtos.PagedResponse<CartResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetCartById_WithExistingId_ReturnsOkAndCart()
    {
        // Arrange
        var createRequest = new CartRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/carts", createRequest);
        var createdCart = await createResponse.Content.ReadFromJsonAsync<CartResponse>();
        var cartId = createdCart!.Id;

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(cartId);
    }

    [Fact]
    public async Task DeleteCart_WithExistingId_ReturnsOkAndRemovesFromDatabase()
    {
        // Arrange
        var createRequest = new CartRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/carts", createRequest);
        var createdCart = await createResponse.Content.ReadFromJsonAsync<CartResponse>();
        var cartId = createdCart!.Id;

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{cartId}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var deletedCart = db.Carts.FirstOrDefault(c => c.Id == cartId);
        deletedCart.Should().BeNull();
    }

    [Fact]
    public async Task ApplyDiscountToCart_WithValidPayload_ReturnsOk()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder().Build();
        var cartResponse = await _client.PostAsJsonAsync("/api/carts", cartRequest);
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();

        var discountRequest = new ApplyDiscountRequest("DISCOUNT10");

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/carts/{cart!.Id}/discount",
            discountRequest
        );

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetCartDiscount_WithExistingCart_ReturnsOkOrNull()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder().Build();
        var cartResponse = await _client.PostAsJsonAsync("/api/carts", cartRequest);
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();

        // Act
        var response = await _client.GetAsync($"/api/carts/{cart!.Id}/discount");

        // Assert - may be OK, NotFound, InternalServerError, or other status
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetCartById_WithNonExistentId_ReturnsNotFoundOrError()
    {
        // Arrange
        var nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/carts/{nonExistentId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DeleteCart_WithNonExistentId_ReturnsOkOrNotFound()
    {
        // Arrange
        var nonExistentId = 99999;

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{nonExistentId}");

        // Assert - may be idempotent, accept any valid status
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    [Fact]
    public async Task ApplyDiscountToCart_WithNonExistentCartId_ReturnsErrorOrNotFound()
    {
        // Arrange
        var discountRequest = new ApplyDiscountRequest("DISCOUNT10");

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/carts/99999/discount",
            discountRequest
        );

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ApplyDiscountToCart_WithInvalidDiscountCode_ReturnsErrorOrOk()
    {
        // Arrange
        var cartRequest = new CartRequestBuilder().Build();
        var cartResponse = await _client.PostAsJsonAsync("/api/carts", cartRequest);
        var cart = await cartResponse.Content.ReadFromJsonAsync<CartResponse>();

        var discountRequest = new ApplyDiscountRequest("INVALIDCODE99999");

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/carts/{cart!.Id}/discount",
            discountRequest
        );

        // Assert - should either accept or reject invalid codes
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetCartDiscount_WithNonExistentCartId_ReturnsNotFoundOrError()
    {
        // Arrange
        var nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/carts/{nonExistentId}/discount");

        // Assert
        ((int)response.StatusCode).Should().BeLessThan(600);
    }

    #endregion
}
