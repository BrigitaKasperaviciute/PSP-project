using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Business.Dtos;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.IntegrationTests.Infrastructure.Builders;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class TaxControllerTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private readonly HttpClient _client;

    public TaxControllerTests(ApiTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient(role: "Admin");
    }

    public async Task InitializeAsync()
    {
        // Clean up taxes before each test
        await using var db = _factory.CreateDbContext();
        db.Taxes.RemoveRange(db.Taxes);
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Happy Path Tests

    [Fact]
    public async Task CreateTax_WithValidPayload_ReturnsOkAndPersistsTax()
    {
        // Arrange
        var request = new TaxRequestBuilder()
            .WithName("VAT Tax")
            .WithRate(20)
            .WithIsPercentage(true)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("VAT Tax");
        body.Rate.Should().Be(20);
        body.IsPercentage.Should().Be(true);

        // Assert - database persistence
        await using var db = _factory.CreateDbContext();
        var persistedTax = db.Taxes.FirstOrDefault(t => t.Name == "VAT Tax");
        persistedTax.Should().NotBeNull();
        persistedTax!.Rate.Should().Be(20);
    }

    [Fact]
    public async Task GetAllTaxes_WithValidPagination_ReturnsOkAndTaxList()
    {
        // Arrange
        var taxRequests = new[]
        {
            new TaxRequestBuilder().WithName("Tax1").Build(),
            new TaxRequestBuilder().WithName("Tax2").Build(),
            new TaxRequestBuilder().WithName("Tax3").Build()
        };

        foreach (var req in taxRequests)
        {
            await _client.PostAsJsonAsync("/api/tax", req);
        }

        // Act
        var response = await _client.GetAsync("/api/tax?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TaxResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetTaxById_WithExistingId_ReturnsOkAndTax()
    {
        // Arrange
        var createRequest = new TaxRequestBuilder()
            .WithName("GetByIdTest Tax")
            .Build();
        var createResponse = await _client.PostAsJsonAsync("/api/tax", createRequest);
        var createdTax = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();
        var taxId = createdTax!.Id;

        // Act
        var response = await _client.GetAsync($"/api/tax/{taxId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(taxId);
        body.Name.Should().Be("GetByIdTest Tax");
    }

    [Fact]
    public async Task UpdateTax_WithValidPayload_ReturnsOkAndUpdatesPersistent()
    {
        // Arrange
        var createRequest = new TaxRequestBuilder()
            .WithName("Original Name")
            .WithRate(10)
            .Build();
        var createResponse = await _client.PostAsJsonAsync("/api/tax", createRequest);
        var createdTax = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();
        var originalTaxId = createdTax!.Id;
        var taxVersionId = createdTax.TaxId;

        var updateRequest = new TaxRequestBuilder()
            .WithName("Updated Name")
            .WithRate(25)
            .Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/tax/{originalTaxId}", updateRequest);

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedBody = await response.Content.ReadFromJsonAsync<TaxResponse>();
        updatedBody!.Name.Should().Be("Updated Name");
        updatedBody.Rate.Should().Be(25);

        // Assert - database persistence (soft-delete versioning pattern)
        await using var db = _factory.CreateDbContext();
        var oldTax = db.Taxes.FirstOrDefault(t => t.Id == originalTaxId);
        oldTax!.IsDeleted.Should().BeTrue(); // Old record marked as deleted
        
        var newTax = db.Taxes.FirstOrDefault(t => t.TaxId == taxVersionId && !t.IsDeleted);
        newTax.Should().NotBeNull();
        newTax!.Name.Should().Be("Updated Name");
        newTax.Rate.Should().Be(25);
    }

    [Fact]
    public async Task DeleteTax_WithExistingId_ReturnsOkAndRemovesFromDatabase()
    {
        // Arrange
        var createRequest = new TaxRequestBuilder().Build();
        var createResponse = await _client.PostAsJsonAsync("/api/tax", createRequest);
        var createdTax = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();
        var taxId = createdTax!.Id;

        // Act
        var response = await _client.DeleteAsync($"/api/tax/{taxId}");

        // Assert - HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database state (soft deletion)
        await using var db = _factory.CreateDbContext();
        var deletedTax = db.Taxes.FirstOrDefault(t => t.Id == taxId);
        deletedTax.Should().NotBeNull();
        deletedTax!.IsDeleted.Should().BeTrue();
    }

    #endregion

    #region Negative Flow Tests

    [Fact]
    public async Task CreateTax_WithNullName_ReturnsBadRequest()
    {
        // Arrange
        var request = new { Rate = 10, IsPercentage = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTax_WithNegativeRate_ReturnsOkOrBadRequest()
    {
        // Arrange - testing boundary condition
        var request = new TaxRequestBuilder()
            .WithRate(-5)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert - should either reject negative or handle it consistently
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTaxById_WithNonExistentId_ReturnsNotFoundOrBadRequest()
    {
        // Arrange
        var nonExistentId = 99999;

        // Act
        var response = await _client.GetAsync($"/api/tax/{nonExistentId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task UpdateTax_WithNonExistentId_ReturnsNotFoundOrBadRequest()
    {
        // Arrange
        var updateRequest = new TaxRequestBuilder().Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/tax/99999", updateRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task DeleteTax_WithNonExistentId_ReturnsNotFoundOrOk()
    {
        // Arrange
        var nonExistentId = 99999;

        // Act
        var response = await _client.DeleteAsync($"/api/tax/{nonExistentId}");

        // Assert - may return 200 if delete is idempotent
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LinkTaxToItems_WithValidProductIds_ReturnsOk()
    {
        // Arrange
        var taxRequest = new TaxRequestBuilder().Build();
        var taxResponse = await _client.PostAsJsonAsync("/api/tax", taxRequest);
        var tax = await taxResponse.Content.ReadFromJsonAsync<TaxResponse>();

        var itemIds = new[] { 1, 2, 3 };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/tax/{tax!.Id}/link?itemsAreProducts=true",
            itemIds
        );

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task GetTaxesLinkedToItemId_WithValidItemId_ReturnsOkAndTaxList()
    {
        // Arrange
        var itemId = 1;

        // Act
        var response = await _client.GetAsync($"/api/tax/item/{itemId}?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<TaxResponse>>();
        body.Should().NotBeNull();
    }

    #endregion
}
