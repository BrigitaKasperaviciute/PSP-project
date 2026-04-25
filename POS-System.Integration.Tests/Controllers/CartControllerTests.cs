using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

// CartController has no [Authorize] attribute, so no JWT is required for most tests.
public class CartControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public CartControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Seeded carts: Id=1..4, all with IsDeleted=false.

    [Fact]
    public async Task GetAll_ValidRequest_ReturnsOkWithPagedResult()
    {
        // Arrange
        // (seeded database has carts)

        // Act
        var response = await _client.GetAsync("/api/carts");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_PageSizeZero_ReturnsOkWithEmptyPage()
    {
        // Arrange
        // (pageSize=0 edge case — service returns an empty results list)

        // Act
        var response = await _client.GetAsync("/api/carts?pageSize=0&pageNum=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithCart()
    {
        // Arrange
        const int existingCartId = 1; // seeded

        // Act
        var response = await _client.GetAsync($"/api/carts/{existingCartId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingCartId);
        body.EmployeeVersionId.Should().Be(1);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsInternalServerError()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/carts/{nonExistentId}");

        // Assert — CartService throws plain Exception (not NotFoundException), so GlobalExceptionHandler maps it to 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCart()
    {
        // Arrange
        var request = new CartRequest { EmployeeVersionId = 1 }; // seeded employee Id=1

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.EmployeeVersionId.Should().Be(1);

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.Carts.FindAsync(body.Id);
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_NullEmployeeVersionId_StillCreatesCart()
    {
        // Arrange – CartRequest only has EmployeeVersionId (int, defaults to 0)
        var request = new CartRequest { EmployeeVersionId = 0 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);

        // Assert – CartController does not validate EmployeeVersionId against FK in code,
        // and the in-memory database does not enforce FK constraints.
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsOk()
    {
        // Arrange – create a cart to delete so we don't affect seeded data
        var created = await (await _client.PostAsJsonAsync("/api/carts", new CartRequest { EmployeeVersionId = 1 }))
            .Content.ReadFromJsonAsync<CartResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deletedCart = await db.Carts.FindAsync(created.Id);
        deletedCart.Should().BeNull();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsInternalServerError()
    {
        // Arrange
        // (nothing to set up)

        // Act
        var response = await _client.DeleteAsync("/api/carts/99999");

        // Assert — CartService throws plain Exception (not NotFoundException), so GlobalExceptionHandler maps it to 500
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetCartDiscount_NoDiscountApplied_ReturnsNoContent()
    {
        // Arrange – seeded cart Id=1 has no discount; Ok(null) in ASP.NET Core 8 returns 204
        const int cartId = 1;

        // Act
        var response = await _client.GetAsync($"/api/carts/{cartId}/discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetCartDiscount_NonExistentCart_ReturnsNoContent()
    {
        // Arrange — CartService.GetCartDiscountAsync returns null when cart has no discount;
        // controller calls Ok(null) which ASP.NET Core 8 serialises as 204 NoContent.
        // Act
        var response = await _client.GetAsync("/api/carts/99999/discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
