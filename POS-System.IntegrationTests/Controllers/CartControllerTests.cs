using System.Net;
using Xunit;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CartControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    // Seeded carts have Ids 1–4; delete test-created carts only
    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        // Remove any cart discounts on test carts first (FK constraint)
        await db.CartItems.Where(ci => ci.CartId > 4).ExecuteDeleteAsync();
        await db.Carts.Where(c => c.Id > 4).ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAll ---------------

    [Fact]
    public async Task GetAll_WhenCartsExist_ReturnsOkWithPagedResults()
    {
        // Arrange – seeded carts are present (no auth needed for Cart endpoints)

        // Act
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithPagination_ReturnsCorrectPageSize()
    {
        // Arrange

        // Act
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().HaveCountLessThanOrEqualTo(2);
    }

    // --------------- GetByID ---------------

    [Fact]
    public async Task GetByID_WhenCartExists_ReturnsOkWithCart()
    {
        // Arrange – seeded Cart Id=1
        const int existingId = 1;

        // Act
        var response = await _client.GetAsync($"/api/carts/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
    }

    [Fact]
    public async Task GetByID_WhenCartDoesNotExist_ReturnsInternalServerError()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/carts/{nonExistentId}");

        // Assert – CartService throws generic Exception (not NotFoundException) → 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // --------------- Create ---------------

    [Fact]
    public async Task Create_WithValidRequest_ReturnsOkAndPersistsCart()
    {
        // Arrange – seeded employee version Id=1
        var request = new CartRequest { EmployeeVersionId = 1 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.EmployeeVersionId.Should().Be(request.EmployeeVersionId);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Carts.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WithInvalidEmployeeVersionId_ReturnsInternalServerError()
    {
        // Arrange – EmployeeVersionId 0 violates FK constraint → DbUpdateException → 500
        var request = new CartRequest { EmployeeVersionId = 0 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // --------------- Delete ---------------

    [Fact]
    public async Task Delete_WhenCartExists_ReturnsOkAndRemovesFromDb()
    {
        // Arrange – cart must be IN_PROGRESS to be deletable
        var created = await CreateCartAsync(employeeVersionId: 1);

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – hard-deleted (CartService calls Repository.Delete, not soft delete)
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Carts.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == created.Id);
        persisted.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WhenCartDoesNotExist_ReturnsInternalServerError()
    {
        // Arrange

        // Act
        var response = await _client.DeleteAsync("/api/carts/99999");

        // Assert – CartService throws generic Exception (not NotFoundException) → 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // --------------- ApplyDiscountToCart ---------------

    [Fact]
    public async Task ApplyDiscountToCart_WithValidDiscountCode_ReturnsOkWithCart()
    {
        // Arrange – create a cart and a cart discount, then apply the discount
        var cart = await CreateCartAsync(employeeVersionId: 1);
        var discountResp = await _client.PostAsJsonAsync("/api/cart-discount",
            new CartDiscountRequest { Value = 15, IsPercentage = true, EndDate = DateTime.UtcNow.AddDays(30) });
        discountResp.EnsureSuccessStatusCode();
        var discount = (await discountResp.Content.ReadFromJsonAsync<CartDiscountResponse>())!;

        // Act
        var response = await _client.PatchAsJsonAsync(
            $"/api/carts/{cart.Id}/discount",
            new ApplyDiscountRequest(discount.Id));

        // Assert – ApplyDiscountForCartAsync returns CartDiscountResponse, not CartResponse
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(discount.Id);
    }

    [Fact]
    public async Task ApplyDiscountToCart_WhenCartDoesNotExist_ReturnsNotFound()
    {
        // Arrange – create a valid discount but use a non-existent cart ID
        var discountResp = await _client.PostAsJsonAsync("/api/cart-discount",
            new CartDiscountRequest { Value = 10, IsPercentage = false, EndDate = DateTime.UtcNow.AddDays(1) });
        discountResp.EnsureSuccessStatusCode();
        var discount = (await discountResp.Content.ReadFromJsonAsync<CartDiscountResponse>())!;

        // Act
        var response = await _client.PatchAsJsonAsync(
            "/api/carts/99999/discount",
            new ApplyDiscountRequest(discount.Id));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- GetCartDiscountAsync ---------------

    [Fact]
    public async Task GetCartDiscountAsync_WhenCartHasDiscount_ReturnsOkWithDiscount()
    {
        // Arrange – create a cart, create a discount, apply it
        var cart = await CreateCartAsync(employeeVersionId: 1);
        var discountResp = await _client.PostAsJsonAsync("/api/cart-discount",
            new CartDiscountRequest { Value = 20, IsPercentage = true, EndDate = DateTime.UtcNow.AddDays(7) });
        discountResp.EnsureSuccessStatusCode();
        var discount = (await discountResp.Content.ReadFromJsonAsync<CartDiscountResponse>())!;
        await _client.PatchAsJsonAsync($"/api/carts/{cart.Id}/discount", new ApplyDiscountRequest(discount.Id));

        // Act
        var response = await _client.GetAsync($"/api/carts/{cart.Id}/discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(discount.Id);
        body.Value.Should().Be(discount.Value);
    }

    [Fact]
    public async Task GetCartDiscountAsync_WhenCartHasNoDiscount_ReturnsNoContent()
    {
        // Arrange – seeded Cart Id=1 has no discount
        const int cartId = 1;

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/discount");

        // Assert – controller returns Ok(null) which ASP.NET serializes as 204 NoContent
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCartDiscountAsync_WhenCartDoesNotExist_ReturnsNoContent()
    {
        // Arrange

        // Act
        var response = await _client.GetAsync("/api/carts/99999/discount");

        // Assert – controller returns Ok(null) which ASP.NET serializes as 204 NoContent
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ---- helpers ----

    private async Task<CartResponse> CreateCartAsync(int employeeVersionId)
    {
        var response = await _client.PostAsJsonAsync("/api/carts",
            new CartRequest { EmployeeVersionId = employeeVersionId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CartResponse>())!;
    }
}
