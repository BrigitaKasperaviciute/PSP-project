using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
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
    public async Task GetAll_WithValidAuth_ReturnsOkAndPagedTaxes()
    {
        // Arrange
        var request = new TaxBuilder().WithName("VAT").WithRate(21).Build();
        await _client.PostAsJsonAsync("/api/tax", request);

        // Act
        var response = await _client.GetAsync("/api/tax?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<TaxResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        // Arrange
        var unauthClient = _factory.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/api/tax");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkAndCorrectTax()
    {
        // Arrange
        var request = new TaxBuilder().WithName("ServiceTax").WithRate(15).Build();
        var createResponse = await _client.PostAsJsonAsync("/api/tax", request);
        var created = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();

        // Act
        var response = await _client.GetAsync($"/api/tax/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("ServiceTax");
        body.Rate.Should().Be(15);
        body.IsPercentage.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Arrange – use an ID that cannot exist after clearing the table
        const int nonExistentId = 999999;

        // Act
        var response = await _client.GetAsync($"/api/tax/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error.Should().NotBeNull();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersistsTax()
    {
        // Arrange
        var request = new TaxBuilder().WithName("NewTax").WithRate(8).WithIsPercentage(false).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/tax", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("NewTax");
        body.Rate.Should().Be(8);
        body.IsPercentage.Should().BeFalse();
        body.IsDeleted.Should().BeFalse();

        // Assert - database state
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Taxes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == body.Id);
        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be("NewTax");
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        // Arrange
        var readOnlyClient = _factory.CreateClientWithClaims("TaxRead");
        var request = new TaxBuilder().Build();

        // Act
        var response = await readOnlyClient.PostAsJsonAsync("/api/tax", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_ReturnsOkAndVersionsTheTax()
    {
        // Arrange
        var original = new TaxBuilder().WithName("Original").WithRate(5).Build();
        var createResponse = await _client.PostAsJsonAsync("/api/tax", original);
        var created = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();

        var updated = new TaxBuilder().WithName("Updated").WithRate(12).Build();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/tax/{created!.Id}", updated);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - new version has updated values
        var body = await response.Content.ReadFromJsonAsync<TaxResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Updated");
        body.Rate.Should().Be(12);
        body.TaxId.Should().Be(created.TaxId);

        // Assert - database: old version is soft-deleted, new version exists
        await using var db = _factory.CreateDbContext();
        var oldVersion = await db.Taxes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == created.Id);
        oldVersion.Should().NotBeNull();
        oldVersion!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        // Arrange
        var request = new TaxBuilder().Build();

        // Act
        var response = await _client.PutAsJsonAsync("/api/tax/999999", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndSoftDeletesTax()
    {
        // Arrange
        var createResponse = await _client.PostAsJsonAsync("/api/tax", new TaxBuilder().Build());
        var created = await createResponse.Content.ReadFromJsonAsync<TaxResponse>();

        // Act
        var response = await _client.DeleteAsync($"/api/tax/{created!.Id}");

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - database: tax is soft-deleted
        await using var db = _factory.CreateDbContext();
        var inDb = await db.Taxes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == created.Id);
        inDb.Should().NotBeNull();
        inDb!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/tax/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Link / Unlink ---

    [Fact]
    public async Task Link_TaxToProduct_ReturnsOkAndCreatesAssociation()
    {
        // Arrange – create a tax and a product to link
        var taxResponse = await _client.PostAsJsonAsync("/api/tax", new TaxBuilder().Build());
        var tax = await taxResponse.Content.ReadFromJsonAsync<TaxResponse>();

        var productClient = _factory.CreateClientWithClaims("TaxRead", "TaxWrite", "ItemRead", "ItemWrite");
        var productResponse = await productClient.PostAsJsonAsync("/api/product", new ProductBuilder().Build());
        var product = await productResponse.Content.ReadFromJsonAsync<ProductResponse>();

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/tax/{tax!.Id}/link?itemsAreProducts=true",
            new[] { product!.Id });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _factory.CreateDbContext();
        // ProductOnTax inherits BaseManyToManyEntity<Product, Tax>: LeftEntityId=ProductId, RightEntityId=TaxId
        var link = await db.ProductOnTaxes.AsNoTracking()
            .AnyAsync(pt => pt.RightEntityId == tax.Id && pt.LeftEntityId == product.Id);
        link.Should().BeTrue();
    }

    [Fact]
    public async Task GetTaxesLinkedToItem_WithExistingProduct_ReturnsOk()
    {
        // Arrange – create tax, product, and link them
        var tax = await (await _client.PostAsJsonAsync("/api/tax", new TaxBuilder().Build()))
            .Content.ReadFromJsonAsync<TaxResponse>();
        var product = await (await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();
        await _client.PutAsJsonAsync($"/api/tax/{tax!.Id}/link?itemsAreProducts=true", new[] { product!.Id });

        // Act
        var response = await _client.GetAsync($"/api/tax/item/{product.Id}?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var taxes = await response.Content.ReadFromJsonAsync<IEnumerable<TaxResponse>>();
        taxes.Should().NotBeNull();
        taxes!.Should().Contain(t => t.Id == tax.Id);
    }
}

// Minimal paged-response projection for deserialization
file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
