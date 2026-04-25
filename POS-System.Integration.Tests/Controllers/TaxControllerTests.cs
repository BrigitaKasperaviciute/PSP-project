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

public class TaxControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public TaxControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // Seeded active taxes: Id=2 (Tax2, Rate=10, IsPercentage=true) and Id=4 (Tax1 v2, Rate=199, IsPercentage=false)

    [Fact]
    public async Task GetAll_ValidToken_ReturnsOkWithPagedResult()
    {
        // Arrange
        // (seeded database contains active taxes with IsDeleted=false)

        // Act
        var response = await _authClient.GetAsync("/api/tax");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TaxResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no Authorization header on _anonClient)

        // Act
        var response = await _anonClient.GetAsync("/api/tax");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOkWithTax()
    {
        // Arrange
        const int existingTaxId = 2; // seeded Tax2

        // Act
        var response = await _authClient.GetAsync($"/api/tax/{existingTaxId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingTaxId);
        body.Name.Should().Be("Tax2");
        body.Rate.Should().Be(10);
    }

    [Fact]
    public async Task GetById_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _authClient.GetAsync($"/api/tax/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithCreatedTax()
    {
        // Arrange
        var request = new TaxRequest { Name = "IntegrationTax", Rate = 15, IsPercentage = true };

        // Act
        var response = await _authClient.PostAsJsonAsync("/api/tax", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("IntegrationTax");
        body.Rate.Should().Be(15);
        body.IsPercentage.Should().BeTrue();
        body.IsDeleted.Should().BeFalse();

        // Verify database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedTax = await db.Taxes.FirstOrDefaultAsync(t => t.Name == "IntegrationTax");
        savedTax.Should().NotBeNull();
        savedTax!.Rate.Should().Be(15);
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = new TaxRequest { Name = "UnauthorizedTax", Rate = 5, IsPercentage = false };

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/tax", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ExistingId_ReturnsOkWithUpdatedTax()
    {
        // Arrange – create a tax to update so the test is independent
        var createRequest = new TaxRequest { Name = "TaxToUpdate", Rate = 10, IsPercentage = true };
        var createResponse = await _authClient.PostAsJsonAsync("/api/tax", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();
        var updateRequest = new TaxRequest { Name = "TaxUpdated", Rate = 20, IsPercentage = false };

        // Act
        var response = await _authClient.PutAsJsonAsync($"/api/tax/{created!.Id}", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("TaxUpdated");
        body.Rate.Should().Be(20);
        body.IsPercentage.Should().BeFalse();

        // Verify old tax was soft-deleted in database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var oldTax = await db.Taxes.FindAsync(created.Id);
        oldTax!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new TaxRequest { Name = "Ghost", Rate = 1, IsPercentage = false };

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/tax/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsOkAndMarksTaxDeleted()
    {
        // Arrange – create a dedicated tax to delete
        var createRequest = new TaxRequest { Name = "TaxToDelete", Rate = 5, IsPercentage = true };
        var created = await (await _authClient.PostAsJsonAsync("/api/tax", createRequest))
            .Content.ReadFromJsonAsync<TaxResponse>();

        // Act
        var response = await _authClient.DeleteAsync($"/api/tax/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var deletedTax = await db.Taxes.FindAsync(created.Id);
        deletedTax!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsNotFound()
    {
        // Arrange
        // (no setup needed)

        // Act
        var response = await _authClient.DeleteAsync("/api/tax/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkToProducts_ValidIds_ReturnsOk()
    {
        // Arrange – create fresh tax and product IDs to link
        var taxResp = await (await _authClient.PostAsJsonAsync("/api/tax",
            new TaxRequest { Name = "LinkTax", Rate = 5, IsPercentage = true }))
            .Content.ReadFromJsonAsync<TaxResponse>();
        var productIds = new[] { 4 }; // seeded active product Id=4

        // Act
        var response = await _authClient.PutAsJsonAsync(
            $"/api/tax/{taxResp!.Id}/link?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkToProducts_NonExistentTax_ReturnsOk()
    {
        // Arrange — ManyToManyService.LinkItemToItemsAsync silently succeeds when the source item
        // is not found, so the controller returns 200.
        var productIds = new[] { 4 };

        // Act
        var response = await _authClient.PutAsJsonAsync(
            "/api/tax/99999/link?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTaxesLinkedToItemId_ValidProductId_ReturnsOkWithList()
    {
        // Arrange
        const int productId = 4; // seeded active product

        // Act
        var response = await _authClient.GetAsync(
            $"/api/tax/item/{productId}?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<TaxResponse>>();
        body.Should().NotBeNull();
    }

    [Fact]
    public async Task UnlinkFromProducts_ValidIds_ReturnsOk()
    {
        // Arrange – create a tax so there's something to unlink from
        var taxResp = await (await _authClient.PostAsJsonAsync("/api/tax",
            new TaxRequest { Name = "UnlinkTax", Rate = 3, IsPercentage = true }))
            .Content.ReadFromJsonAsync<TaxResponse>();
        var productIds = new[] { 4 }; // seeded active product

        // Act
        var response = await _authClient.PutAsJsonAsync(
            $"/api/tax/{taxResp!.Id}/unlink?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTaxesLinkedToItemId_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no auth token)

        // Act
        var response = await _anonClient.GetAsync("/api/tax/item/4?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
