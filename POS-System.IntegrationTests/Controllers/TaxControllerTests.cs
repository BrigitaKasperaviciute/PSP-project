using System.Net;
using Microsoft.AspNetCore.Http;
using Xunit;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class TaxControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public TaxControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    // Remove any taxes created by previous tests (seeded taxes have Ids 1–4)
    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.Taxes.Where(t => t.Id > 4).ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --------------- GetAllTaxes ---------------

    [Fact]
    public async Task GetAllTaxes_WhenTaxesExist_ReturnsOkWithPagedResults()
    {
        // Arrange – seeded data already present

        // Act
        var response = await _client.GetAsync("/api/tax?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TaxResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
        body.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAllTaxes_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/tax?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --------------- GetTaxById ---------------

    [Fact]
    public async Task GetTaxById_WhenTaxExists_ReturnsOkWithTax()
    {
        // Arrange – seeded Tax with Id=2 (TaxId=2, Name="Tax2", IsDeleted=false)
        const int existingId = 2;

        // Act
        var response = await _client.GetAsync($"/api/tax/{existingId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(existingId);
        body.Name.Should().Be("Tax2");
    }

    [Fact]
    public async Task GetTaxById_WhenTaxDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/tax/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.Status.Should().Be(StatusCodes.Status404NotFound);
    }

    // --------------- CreateTax ---------------

    [Fact]
    public async Task CreateTax_WithValidRequest_ReturnsOkAndPersistsTax()
    {
        // Arrange
        var request = new TaxRequest
        {
            Name = "GST",
            Rate = 18,
            IsPercentage = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(request.Name);
        body.Rate.Should().Be(request.Rate);
        body.IsPercentage.Should().Be(request.IsPercentage);

        // Assert – database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Taxes.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(request.Name);
        persisted.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTax_WithMissingName_ReturnsBadRequest()
    {
        // Arrange – Name is required; send empty
        var payload = new { Rate = 10, IsPercentage = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // --------------- UpdateTaxById ---------------

    [Fact]
    public async Task UpdateTaxById_WithValidRequest_ReturnsOkAndUpdates()
    {
        // Arrange – create a tax to update
        var created = await CreateTaxAsync("UpdateMe", 5, true);

        var updateRequest = new TaxRequest
        {
            Name = "UpdatedTax",
            Rate = 25,
            IsPercentage = false
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/tax/{created.Id}", updateRequest);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(updateRequest.Name);
        body.Rate.Should().Be(updateRequest.Rate);

        // Assert – a new version row was inserted (versioning pattern)
        await using var db = _factory.CreateDbContext();
        var versions = await db.Taxes.AsNoTracking()
            .Where(t => t.TaxId == created.TaxId)
            .ToListAsync();
        versions.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task UpdateTaxById_WhenTaxDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var request = new TaxRequest { Name = "X", Rate = 1, IsPercentage = true };

        // Act
        var response = await _client.PutAsJsonAsync("/api/tax/99999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- DeleteTaxById ---------------

    [Fact]
    public async Task DeleteTaxById_WhenTaxExists_ReturnsOkAndSoftDeletes()
    {
        // Arrange
        var created = await CreateTaxAsync("DeleteMe", 7, true);

        // Act
        var response = await _client.DeleteAsync($"/api/tax/{created.Id}");

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert – soft-deleted in database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Taxes.AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == created.Id);
        persisted.Should().NotBeNull();
        persisted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTaxById_WhenTaxDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        const int nonExistentId = 99999;

        // Act
        var response = await _client.DeleteAsync($"/api/tax/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --------------- LinkTaxToItems / UnlinkTaxFromItems ---------------

    [Fact]
    public async Task LinkTaxToItems_WithValidProductIds_ReturnsOk()
    {
        // Arrange – seeded product Id=4 (IsDeleted=false), seeded Tax Id=2
        var taxId = 2;
        var productIds = new[] { 4 };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/tax/{taxId}/link?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LinkTaxToItems_WithNonExistentTax_ReturnsOk()
    {
        // Arrange
        var productIds = new[] { 4 };

        // Act
        var response = await _client.PutAsJsonAsync(
            "/api/tax/99999/link?itemsAreProducts=true", productIds);

        // Assert – ManyToManyService silently does nothing when tax not found, returns 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --------------- UnlinkTaxFromItems ---------------

    [Fact]
    public async Task UnlinkTaxFromItems_AfterLinking_ReturnsOk()
    {
        // Arrange – link seeded Product Id=4 to seeded Tax Id=3 first
        const int taxId = 3;
        var productIds = new[] { 4 };
        await _client.PutAsJsonAsync($"/api/tax/{taxId}/link?itemsAreProducts=true", productIds);

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/tax/{taxId}/unlink?itemsAreProducts=true", productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkTaxFromItems_WhenTaxDoesNotExist_ReturnsOk()
    {
        // Arrange
        var productIds = new[] { 4 };

        // Act
        var response = await _client.PutAsJsonAsync(
            "/api/tax/99999/unlink?itemsAreProducts=true", productIds);

        // Assert – ManyToManyService silently does nothing when tax not found, returns 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // --------------- GetTaxesLinkedToItemId ---------------

    [Fact]
    public async Task GetTaxesLinkedToItemId_WhenProductExists_ReturnsOk()
    {
        // Arrange – seeded product Id=4
        const int productId = 4;

        // Act
        var response = await _client.GetAsync($"/api/tax/item/{productId}?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTaxesLinkedToItemId_WhenProductDoesNotExist_ReturnsOkWithEmptyList()
    {
        // Arrange – non-existent product
        const int nonExistentProductId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/tax/item/{nonExistentProductId}?isProduct=true");

        // Assert – service returns empty list for unknown item (no 404)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<TaxResponse>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }

    // ---- helpers ----

    private async Task<TaxResponse> CreateTaxAsync(string name, int rate, bool isPercentage)
    {
        var response = await _client.PostAsJsonAsync("/api/tax",
            new TaxRequest { Name = name, Rate = rate, IsPercentage = isPercentage });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TaxResponse>())!;
    }
}
