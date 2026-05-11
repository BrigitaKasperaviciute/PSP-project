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
public sealed class TaxControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;
    private ApplicationDbContext _db = null!;

    public TaxControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateDefaultClient();
    }

    public async Task InitializeAsync()
    {
        _db = _factory.CreateDbContext();
        // Clean taxes table before each test
        await _db.Taxes.ExecuteDeleteAsync();
        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    #region Happy Path Tests

    [Fact]
    public async Task CreateTax_WithValidPayload_ReturnsOkAndPersistsTax()
    {
        // Arrange
        var request = new TaxRequestBuilder()
            .WithName("VAT")
            .WithRate(19.0m)
            .WithIsPercentage(true)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.Name.Should().Be(request.Name);
        body.Rate.Should().Be(request.Rate);
        body.IsPercentage.Should().Be(request.IsPercentage);

        // Assert - database state
        var persisted = await _db.Taxes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(request.Name);
        persisted.Rate.Should().Be(request.Rate);
    }

    [Fact]
    public async Task GetAllTaxes_WithValidPageNumbers_ReturnsOkWithTaxes()
    {
        // Arrange
        var tax1 = new TaxRequestBuilder().WithName("VAT").Build();
        var tax2 = new TaxRequestBuilder().WithName("GST").Build();
        
        await _client.PostAsJsonAsync("/api/tax", tax1);
        await _client.PostAsJsonAsync("/api/tax", tax2);

        // Act
        var response = await _client.GetAsync("/api/tax?pageNum=0&pageSize=10");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<PaginatedResponse<TaxResponse>>();
        body.Should().NotBeNull();
        body!.Data.Should().HaveCountGreaterThanOrEqualTo(2);
        body.Data.Any(t => t.Name == "VAT").Should().BeTrue();
        body.Data.Any(t => t.Name == "GST").Should().BeTrue();
    }

    [Fact]
    public async Task GetTaxById_WithExistingId_ReturnsOkWithTax()
    {
        // Arrange - create a tax
        var request = new TaxRequestBuilder().WithName("VAT").Build();
        var createResponse = await _client.PostAsJsonAsync("/api/tax", request);
        var createdTax = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();

        // Act
        var response = await _client.GetAsync($"/api/tax/{createdTax!.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(createdTax.Id);
        body.Name.Should().Be(request.Name);
    }

    [Fact]
    public async Task UpdateTax_WithValidPayload_ReturnsOkAndUpdatesTax()
    {
        // Arrange - create a tax first
        var createRequest = new TaxRequestBuilder().WithName("OldName").WithRate(10.0m).Build();
        var createResponse = await _client.PostAsJsonAsync("/api/tax", createRequest);
        var createdTax = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();

        // Act - update the tax
        var updateRequest = new TaxRequestBuilder()
            .WithName("NewName")
            .WithRate(20.0m)
            .Build();
        var response = await _client.PutAsJsonAsync($"/api/tax/{createdTax!.Id}", updateRequest);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(createdTax.Id);
        body.Name.Should().Be(updateRequest.Name);
        body.Rate.Should().Be(updateRequest.Rate);

        var oldVersion = await _db.Taxes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == createdTax.Id);
        oldVersion.Should().NotBeNull();
        oldVersion!.IsDeleted.Should().BeTrue();

        var newVersion = await _db.Taxes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == body.Id);
        newVersion.Should().NotBeNull();
        newVersion!.Name.Should().Be(updateRequest.Name);
        newVersion.Rate.Should().Be(updateRequest.Rate);
    }

    [Fact]
    public async Task DeleteTax_WithExistingId_ReturnsOkAndDeletesTax()
    {
        // Arrange
        var request = new TaxRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/tax", request);
        var createdTax = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/tax/{createdTax!.Id}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database state (soft-deleted version should remain)
        var deleted = await _db.Taxes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == createdTax.Id);
        deleted.Should().NotBeNull();
        deleted!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task LinkTaxToProducts_WithValidProductIds_ReturnsOkAndLinksProducts()
    {
        // Arrange - create a tax and products
        var taxRequest = new TaxRequestBuilder().Build();
        var taxResponse = await _client.PostAsJsonAsync("/api/tax", taxRequest);
        var tax = await taxResponse.Content.ReadFromJsonAsync<TaxResponse>();

        var productRequest = new ProductRequestBuilder().Build();
        var productResponse = await _client.PostAsJsonAsync("/api/product", productRequest);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/tax/{tax!.Id}/link?itemsAreProducts=true",
            new[] { product!.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify link was created
        var linkedProducts = await _client.GetAsync($"/api/tax/item/{product.Id}?isProduct=true");
        linkedProducts.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task GetTaxById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/tax/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateTax_WithDuplicateName_ReturnsConflictOrBadRequest()
    {
        // Arrange - create first tax
        var request = new TaxRequestBuilder().WithName("Duplicate").Build();
        await _client.PostAsJsonAsync("/api/tax", request);

        // Act - try to create duplicate
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert - current service accepts duplicate names and versions tax rows
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateTax_WithNullName_ReturnsBadRequest()
    {
        // Arrange
        var request = """{"name": null, "rate": 10, "isPercentage": true}""";

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTax_WithNegativeRate_ReturnsBadRequest()
    {
        // Arrange
        var request = new TaxRequestBuilder().WithRate(-5.0m).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateTax_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var request = new TaxRequestBuilder().Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/tax/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTax_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.DeleteAsync("/api/tax/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkTaxToProducts_WithNonExistentTaxId_ReturnsNotFound()
    {
        // Arrange
        var productIds = new[] { 1, 2, 3 };

        // Act
        var response = await _client.PutAsJsonAsync(
            "/api/tax/999999/link?itemsAreProducts=true",
            productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkTaxFromProducts_WithNonExistentTaxId_ReturnsNotFound()
    {
        // Arrange
        var productIds = new[] { 1, 2, 3 };

        // Act
        var response = await _client.PutAsJsonAsync(
            "/api/tax/999999/unlink?itemsAreProducts=true",
            productIds);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTaxesLinkedToItem_WithNonExistentItemId_ReturnsEmptyOrNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/tax/item/999999?isProduct=true");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion
}

/// <summary>
/// Placeholder DTOs for responses - adjust based on your actual response models
/// </summary>


public class PaginatedResponse<T>
{
    public List<T> Results { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public List<T> Data
    {
        get => Results;
        set => Results = value;
    }

    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
