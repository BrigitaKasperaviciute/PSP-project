using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

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
        _client = factory.CreateClientWithClaims("TaxRead", "TaxWrite", "ItemRead", "ItemWrite");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.ServiceOnTaxes.ExecuteDeleteAsync();
        await db.Taxes.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_WithValidAuth_ReturnsOkWithPagedResults()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/tax", new TaxBuilder().WithName("VAT").WithRate(21).Build());

        // Act
        var response = await _client.GetAsync("/api/tax");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<TaxResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().Contain(t => t.Name == "VAT");
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/tax");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsTax()
    {
        // Arrange
        var request = new TaxBuilder().WithName("GST").WithRate(15).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().BeGreaterThan(0);
        body.Name.Should().Be("GST");
        body.Rate.Should().Be(15);
        body.IsDeleted.Should().BeFalse();

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Taxes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("GST");
    }

    [Fact]
    public async Task Create_WithoutRequiredClaim_Returns403()
    {
        var response = await _factory.CreateClientWithClaims("TaxRead")
            .PostAsJsonAsync("/api/tax", new TaxBuilder().WithName("Blocked").Build());
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingTax_ReturnsOkWithTax()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/tax",
                new TaxBuilder().WithName("Service Tax").WithRate(8).Build()))
            .Content.ReadFromJsonAsync<TaxResponse>();

        // Act
        var response = await _client.GetAsync($"/api/tax/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body!.Id.Should().Be(created.Id);
        body.Name.Should().Be("Service Tax");
        body.Rate.Should().Be(8);
    }

    [Fact]
    public async Task GetById_WithNonExistingId_Returns404()
    {
        var response = await _client.GetAsync("/api/tax/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ErrorDetails>())!.Status.Should().Be(404);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithExistingTax_ReturnsOkAndSoftDeletesOldVersion()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/tax",
                new TaxBuilder().WithName("Old Tax").WithRate(5).Build()))
            .Content.ReadFromJsonAsync<TaxResponse>();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/tax/{created!.Id}",
            new TaxBuilder().WithName("Updated Tax").WithRate(12).WithIsPercentage(false).Build());

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body!.Name.Should().Be("Updated Tax");
        body.Rate.Should().Be(12);

        // Assert - old version soft-deleted
        await using var db = _factory.CreateDbContext();
        var oldVersion = await db.Taxes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == created.Id);
        oldVersion!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistingId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/tax/999999",
            new TaxBuilder().WithName("Ghost").Build());
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingTax_ReturnsOkAndSoftDeletes()
    {
        // Arrange
        var created = await (await _client.PostAsJsonAsync("/api/tax",
                new TaxBuilder().WithName("Delete Me").Build()))
            .Content.ReadFromJsonAsync<TaxResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/tax/{created!.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database: soft deleted
        await using var db = _factory.CreateDbContext();
        var inDb = await db.Taxes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == created.Id);
        inDb!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistingId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/tax/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Link / Unlink ---

    [Fact]
    public async Task LinkToProduct_WithValidIds_ReturnsOkAndCreatesLink()
    {
        // Arrange – create a product so we don't depend on seeded data that other tests may delete
        var product = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();
        var tax = await (await _client.PostAsJsonAsync("/api/tax", new TaxBuilder().Build()))
            .Content.ReadFromJsonAsync<TaxResponse>();

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/tax/{tax!.Id}/link?itemsAreProducts=true", new[] { product!.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = _factory.CreateDbContext();
        (await db.ProductOnTaxes.AsNoTracking()
            .AnyAsync(p => p.LeftEntityId == product.Id && p.RightEntityId == tax.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task LinkToProduct_WithNonExistingTax_Returns200WithoutCreatingLink()
    {
        // ManyToManyService silently skips when the main entity (tax) does not exist
        var response = await _client.PutAsJsonAsync(
            "/api/tax/999999/link?itemsAreProducts=true", new[] { 1 });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UnlinkFromProduct_WithLinkedProduct_ReturnsOkAndRemovesLink()
    {
        // Arrange – create a product so we don't depend on seeded data that other tests may delete
        var product = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();
        var tax = await (await _client.PostAsJsonAsync("/api/tax", new TaxBuilder().Build()))
            .Content.ReadFromJsonAsync<TaxResponse>();
        await _client.PutAsJsonAsync($"/api/tax/{tax!.Id}/link?itemsAreProducts=true", new[] { product!.Id });

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/tax/{tax.Id}/unlink?itemsAreProducts=true", new[] { product.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await using var db = _factory.CreateDbContext();
        (await db.ProductOnTaxes.AsNoTracking()
            .AnyAsync(p => p.LeftEntityId == product.Id && p.RightEntityId == tax.Id && p.EndDate == null)).Should().BeFalse();
    }

    [Fact]
    public async Task GetLinkedToItem_WithExistingProductId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/tax/item/1?isProduct=true");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
