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

public class ItemDiscountControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public ItemDiscountControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Seeded ItemDiscounts: Id=2 (ItemDiscountId=2, Value=15, IsDeleted=false) and Id=3 (Id=3, IsDeleted=false) are active.

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange
        // (seeded item discounts exist)

        // Act
        var response = await _authClient.GetAsync("/api/item-discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ItemDiscountResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/item-discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithItemDiscount()
    {
        // Arrange
        const int existingId = 2; // seeded, IsDeleted=false

        // Act
        var response = await _authClient.GetAsync($"/api/item-discount/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.Value.Should().Be(15);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _authClient.GetAsync($"/api/item-discount/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCreatedItemDiscount()
    {
        // Arrange
        var request = new ItemDiscountRequest
        {
            Value = 25,
            IsPercentage = true,
            Description = "Integration discount",
            StartDate = null,
            EndDate = null
        };

        // Act
        var response = await _authClient.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(25);
        body.Description.Should().Be("Integration discount");

        // Verify persistence
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var saved = await db.ItemDiscounts.FirstOrDefaultAsync(d => d.Description == "Integration discount");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new ItemDiscountRequest { Value = 10, IsPercentage = false, Description = "x", StartDate = null, EndDate = null };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/item-discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedItemDiscount()
    {
        // Arrange – create a discount to update
        var createReq = new ItemDiscountRequest { Value = 10, IsPercentage = true, Description = "ToUpdate", StartDate = null, EndDate = null };
        var created = await (await _authClient.PostAsJsonAsync("/api/item-discount", createReq))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();
        var updateReq = new ItemDiscountRequest { Value = 30, IsPercentage = false, Description = "Updated", StartDate = null, EndDate = null };

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/item-discount/{created!.Id}", updateReq);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ItemDiscountResponse>();
        body.Should().NotBeNull();
        body!.Value.Should().Be(30);
        body.Description.Should().Be("Updated");
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new ItemDiscountRequest { Value = 5, IsPercentage = false, Description = "x", StartDate = null, EndDate = null };

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/item-discount/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsOkAndSoftDeletesDiscount()
    {
        // Arrange – create a discount to delete
        var createReq = new ItemDiscountRequest { Value = 5, IsPercentage = true, Description = "ToDelete", StartDate = null, EndDate = null };
        var created = await (await _authClient.PostAsJsonAsync("/api/item-discount", createReq))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();

        // Act
        var response = await _authClient.DeleteAsync($"/api/item-discount/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deleted = await db.ItemDiscounts.FindAsync(created.Id);
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        // (nothing to set up)

        // Act
        var response = await _authClient.DeleteAsync("/api/item-discount/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkToProducts_ValidIds_ReturnsOk()
    {
        // Arrange – create a discount and link it to a product
        var createReq = new ItemDiscountRequest { Value = 10, IsPercentage = true, Description = "LinkDiscount", StartDate = null, EndDate = null };
        var created = await (await _authClient.PostAsJsonAsync("/api/item-discount", createReq))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();
        var productIds = new[] { 4 }; // seeded active product

        // Act
        var response = await _authClient.PutAsJsonAsync(
            $"/api/item-discount/{created!.Id}/link?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkToProducts_NonExistentDiscount_ReturnsOk()
    {
        // Arrange — ManyToManyService.LinkItemToItemsAsync silently succeeds when the source item
        // is not found (null check skips the link block without throwing), so the controller returns 200.
        var productIds = new[] { 4 };

        // Act
        var response = await _authClient.PutAsJsonAsync(
            "/api/item-discount/99999/link?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetItemDiscountsLinkedToItemId_ValidProductId_ReturnsOkWithList()
    {
        // Arrange
        const int productId = 4;

        // Act
        var response = await _authClient.GetAsync($"/api/item-discount/item/{productId}?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ItemDiscountResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task UnlinkFromProducts_ValidIds_ReturnsOk()
    {
        // Arrange – create a discount so there's something to unlink from
        var createReq = new ItemDiscountRequest { Value = 10, IsPercentage = true, Description = "UnlinkDiscount", StartDate = null, EndDate = null };
        var created = await (await _authClient.PostAsJsonAsync("/api/item-discount", createReq))
            .Content.ReadFromJsonAsync<ItemDiscountResponse>();
        var productIds = new[] { 4 }; // seeded active product

        // Act
        var response = await _authClient.PutAsJsonAsync(
            $"/api/item-discount/{created!.Id}/unlink?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetItemDiscountsLinkedToItemId_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/item-discount/item/4?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
