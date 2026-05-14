using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ProductModificationControllerIntegrationTests
{
    [Fact]
    public async Task GetAll_WithReadClaim_ReturnsPagedProductModifications()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        var response = await client.GetAsync("/api/product-modification?pageNumber=0&pageSize=10&onlyActive=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductModificationResponse?>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_WithReadClaim_ReturnsProductModification()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        var response = await client.GetAsync("/api/product-modification/4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(4);
        body.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetVersions_WithReadClaim_ReturnsVersionHistory()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        var response = await client.GetAsync("/api/product-modification/1/versions/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>();
        body.Should().NotBeNull();
        body.Should().Contain(version => version!.ProductModificationId == 1);
    }

    [Fact]
    public async Task Create_WithWriteClaim_PersistsProductModification()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemWrite" });

        var countBefore = await factory.ExecuteDbContextAsync(db => db.ProductModifications.CountAsync());
        var payload = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name = "Integration Modification",
            Description = "Created in integration tests",
            Price = 150
        };

        var response = await client.PostAsJsonAsync("/api/product-modification", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(payload.Name);
        body.ProductVersionId.Should().Be(payload.ProductVersionId);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.ProductModifications.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task Update_WithWriteClaim_VersionsProductModification()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemWrite" });

        var payload = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name = "Updated Modification",
            Description = "Updated description",
            Price = 175
        };

        var response = await client.PutAsJsonAsync("/api/product-modification/4", payload);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be(payload.Name);
        body.IsDeleted.Should().BeFalse();

        var oldVersion = await factory.ExecuteDbContextAsync(db => db.ProductModifications.SingleAsync(modification => modification.Id == 4));
        oldVersion.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_WithWriteClaim_MarksProductModificationDeleted()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemWrite" });

        var response = await client.DeleteAsync("/api/product-modification/4");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductModificationResponse>();
        body.Should().NotBeNull();
        body!.IsDeleted.Should().BeTrue();

        var deleted = await factory.ExecuteDbContextAsync(db => db.ProductModifications.SingleAsync(modification => modification.Id == 4));
        deleted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetLinkedToProductId_WithReadClaim_ReturnsActiveVersions()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        var response = await client.GetAsync("/api/product-modification/product/1?pageNumber=0&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductModificationResponse?>>();
        body.Should().NotBeNull();
        body!.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetLinkedToCartItemId_WithReadClaim_ReturnsLinkedModifications()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        var response = await client.GetAsync("/api/product-modification/cart-item/2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<ProductModificationResponse>>();
        body.Should().NotBeNull();
    }
}
