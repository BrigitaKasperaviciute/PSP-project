using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class ProductControllerIntegrationTests
{
    [Fact]
    public async Task GetAll_WithReadClaim_ReturnsPagedProducts()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        // Act
        var response = await client.GetAsync("/api/product?pageSize=10&pageNumber=0");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse?>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_WithReadClaim_ReturnsProduct()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        // Act
        var response = await client.GetAsync("/api/product/4");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(4);
    }

    [Fact]
    public async Task GetVersions_WithReadClaim_ReturnsAllVersionsForProductId()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemRead" });

        // Act
        var response = await client.GetAsync("/api/product/1/versions/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ProductResponse>>();
        body.Should().NotBeNull();
        body!.Should().NotBeEmpty();
        body.Should().OnlyContain(product => product.ProductId == 1);
    }

    [Fact]
    public async Task CreateProduct_ValidClaimAndPayload_PersistsProductAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemWrite" });

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Products.CountAsync());
        var payload = new
        {
            name = "Integration Product",
            description = "Integration test product",
            price = 459,
            imageURL = "https://example.com/product.png",
            stock = 10
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/product", payload);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert - response body
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("name").GetString().Should().Be("Integration Product");

        // Assert - database
        var countAfter = await factory.ExecuteDbContextAsync(db => db.Products.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task DeleteProduct_WithWriteClaim_MarksProductAsDeleted()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "ItemWrite" });

        // Arrange
        var created = await (await client.PostAsJsonAsync("/api/product", new
        {
            name = "Delete Me",
            description = "Delete test product",
            price = 199,
            imageURL = "https://example.com/delete.png",
            stock = 4
        })).Content.ReadFromJsonAsync<ProductResponse>();

        created.Should().NotBeNull();

        // Act
        var response = await client.DeleteAsync($"/api/product/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var persisted = await factory.ExecuteDbContextAsync(db => db.Products.SingleAsync(product => product.Id == created.Id));
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task CreateProduct_MissingClaim_ReturnsForbiddenAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClientWithoutClaims();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Products.CountAsync());
        var payload = new
        {
            name = "Forbidden Product",
            description = "Should be blocked",
            price = 123,
            imageURL = "https://example.com/product.png",
            stock = 3
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/product", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Products.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
