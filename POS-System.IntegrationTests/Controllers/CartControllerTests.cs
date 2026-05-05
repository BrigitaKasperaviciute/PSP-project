using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.Common.Enums;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    // CartController has no [Authorize] – use a plain client.
    private readonly HttpClient _client;

    public CartControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.ServiceReservations.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.Carts.ExecuteUpdateAsync(s => s.SetProperty(c => c.CartDiscountId, (string?)null));
        await db.Carts.ExecuteDeleteAsync();
        await db.CardDiscounts.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_ReturnsOkWithPagedCarts()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build());

        // Act
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<CartResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCorrectCart()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartResponse>();

        // Act
        var response = await _client.GetAsync($"/api/carts/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(created.Id);
        body.Status.Should().Be(CartStatusEnum.PENDING);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/carts/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidEmployee_ReturnsOkAndPersistsCart()
    {
        // Arrange – employee with ID 1 is always seeded
        var request = new CartBuilder().WithEmployeeVersionId(1).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.EmployeeVersionId.Should().Be(1);
        body.Status.Should().Be(CartStatusEnum.PENDING);
        body.CartDiscountId.Should().BeNull();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Carts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(CartStatusEnum.PENDING);
    }

    [Fact]
    public async Task Create_WithNonExistentEmployee_Returns404()
    {
        // Arrange
        var request = new CartBuilder().WithEmployeeVersionId(999999).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndRemovesCart()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{created!.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var inDb = await db.Carts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/carts/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- ApplyDiscount ---

    [Fact]
    public async Task ApplyDiscount_WithValidDiscountCode_ReturnsOkAndLinksDiscount()
    {
        // Arrange – create a cart and a cart discount
        var cart = await (await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartResponse>();
        var discount = await (await _client.PostAsJsonAsync("/api/cart-discount", new CartDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDiscountResponse>();

        var request = new ApplyDiscountRequest(discount!.Id);

        // Act
        var response = await _client.PatchAsJsonAsync($"/api/carts/{cart!.Id}/discount", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var updatedCart = await db.Carts.AsNoTracking().SingleAsync(c => c.Id == cart.Id);
        updatedCart.CartDiscountId.Should().Be(discount.Id);
    }

    [Fact]
    public async Task ApplyDiscount_WithNonExistentCart_Returns404()
    {
        // Arrange
        var discount = await (await _client.PostAsJsonAsync("/api/cart-discount", new CartDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDiscountResponse>();

        // Act
        var response = await _client.PatchAsJsonAsync(
            "/api/carts/999999/discount",
            new ApplyDiscountRequest(discount!.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GetCartDiscount ---

    [Fact]
    public async Task GetCartDiscount_WithAppliedDiscount_ReturnsDiscount()
    {
        // Arrange
        var cart = await (await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartResponse>();
        var discount = await (await _client.PostAsJsonAsync("/api/cart-discount", new CartDiscountBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDiscountResponse>();
        await _client.PatchAsJsonAsync($"/api/carts/{cart!.Id}/discount", new ApplyDiscountRequest(discount!.Id));

        // Act
        var response = await _client.GetAsync($"/api/carts/{cart.Id}/discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(discount.Id);
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
