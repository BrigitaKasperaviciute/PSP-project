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
public sealed class ProductModificationControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public ProductModificationControllerTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClientWithClaims("ItemRead", "ItemWrite");
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.ProductModifications.ExecuteDeleteAsync();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> CreateProductVersionIdAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/product", new ProductBuilder().Build());
        var product = await resp.Content.ReadFromJsonAsync<ProductResponse>();
        return product!.Id;
    }

    private ProductModificationRequest BuildRequest(int productVersionId) => new()
    {
        ProductVersionId = productVersionId,
        Name = $"Mod-{Guid.NewGuid().ToString("N")[..6]}",
        Description = "Test modification",
        Price = 50
    };

    // --- GetAll ---

    [Fact]
    public async Task GetAll_ReturnsOkAndPagedResponse()
    {
        var productVersionId = await CreateProductVersionIdAsync();
        await _client.PostAsJsonAsync("/api/product-modification", BuildRequest(productVersionId));

        var response = await _client.GetAsync("/api/product-modification?pageSize=10&pageNumber=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAll_WithoutAuth_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/api/product-modification");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkAndCorrectModification()
    {
        var productVersionId = await CreateProductVersionIdAsync();
        var createResp = await _client.PostAsJsonAsync("/api/product-modification", BuildRequest(productVersionId));
        var created = await createResp.Content.ReadFromJsonAsync<ProductModificationResponse>();

        var response = await _client.GetAsync($"/api/product-modification/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body!.Id.Should().Be(created.Id);
        body.ProductVersionId.Should().Be(productVersionId);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        var response = await _client.GetAsync("/api/product-modification/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GetVersions ---

    [Fact]
    public async Task GetVersions_ReturnsAllVersionsForSameLogicalModification()
    {
        var productVersionId = await CreateProductVersionIdAsync();
        var created = await (await _client.PostAsJsonAsync("/api/product-modification", BuildRequest(productVersionId)))
            .Content.ReadFromJsonAsync<ProductModificationResponse>();

        var response = await _client.GetAsync($"/api/product-modification/{created!.ProductModificationId}/versions/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Should().Contain(m => m.Id == created.Id);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidPayload_ReturnsOkAndPersists()
    {
        var productVersionId = await CreateProductVersionIdAsync();
        var request = BuildRequest(productVersionId);

        var response = await _client.PostAsJsonAsync("/api/product-modification", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(request.Name);
        body.Price.Should().Be(50);
        body.IsDeleted.Should().BeFalse();

        await using var db = _factory.CreateDbContext();
        var inDb = await db.ProductModifications.AsNoTracking().SingleOrDefaultAsync(m => m.Id == body.Id);
        inDb.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_WithoutWriteClaim_Returns403()
    {
        var readClient = _factory.CreateClientWithClaims("ItemRead");
        var response = await readClient.PostAsJsonAsync("/api/product-modification",
            new ProductModificationRequest { ProductVersionId = 1, Name = "X", Description = "X", Price = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // --- Update ---

    [Fact]
    public async Task Update_WithValidPayload_CreatesNewVersionAndSoftDeletesOld()
    {
        var productVersionId = await CreateProductVersionIdAsync();
        var created = await (await _client.PostAsJsonAsync("/api/product-modification", BuildRequest(productVersionId)))
            .Content.ReadFromJsonAsync<ProductModificationResponse>();

        var updateRequest = BuildRequest(productVersionId) with { Name = "Updated", Price = 99 };
        var response = await _client.PutAsJsonAsync($"/api/product-modification/{created!.Id}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body!.Name.Should().Be("Updated");
        body.Price.Should().Be(99);
        body.ProductModificationId.Should().Be(created.ProductModificationId);

        await using var db = _factory.CreateDbContext();
        var old = await db.ProductModifications.AsNoTracking().SingleOrDefaultAsync(m => m.Id == created.Id);
        old!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Update_WithNonExistentId_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/product-modification/999999",
            new ProductModificationRequest { ProductVersionId = 1, Name = "X", Description = "X", Price = 1 });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOkAndSoftDeletes()
    {
        var productVersionId = await CreateProductVersionIdAsync();
        var created = await (await _client.PostAsJsonAsync("/api/product-modification", BuildRequest(productVersionId)))
            .Content.ReadFromJsonAsync<ProductModificationResponse>();

        var response = await _client.DeleteAsync($"/api/product-modification/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _factory.CreateDbContext();
        var inDb = await db.ProductModifications.AsNoTracking().SingleOrDefaultAsync(m => m.Id == created.Id);
        inDb!.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/product-modification/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GetLinkedToProduct ---

    [Fact]
    public async Task GetLinkedToProduct_ReturnsOkAndModificationsForProduct()
    {
        var productVersionId = await CreateProductVersionIdAsync();
        await _client.PostAsJsonAsync("/api/product-modification", BuildRequest(productVersionId));

        var response = await _client.GetAsync($"/api/product-modification/product/{productVersionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponseDto<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }
}

file record PagedResponseDto<T>(int TotalCount, int PageSize, int PageNum, IEnumerable<T> Results);
