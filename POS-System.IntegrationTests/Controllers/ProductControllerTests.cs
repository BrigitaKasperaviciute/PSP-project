using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection("Integration tests")]
public sealed class ProductControllerTests : IntegrationTestBase
{
    public ProductControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ProductScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/product?pageNumber=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/product/4")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/product/1/versions/")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new ProductRequest
        {
            Name = "Integration Product",
            Description = "Integration product description",
            Price = 109,
            Stock = 3,
            ImageURL = "https://example.com/product.png"
        };

        (await client.PostAsJsonAsync("/api/product", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        Product createdProduct = await WithDbContextAsync(async context =>
            await context.Products.SingleAsync(product => product.Name == createRequest.Name));

        var updateRequest = new ProductRequest
        {
            Name = "Integration Product Updated",
            Description = "Updated integration product description",
            Price = 209,
            Stock = 5,
            ImageURL = "https://example.com/product-updated.png"
        };

        (await client.PutAsJsonAsync($"/api/product/{createdProduct.Id}", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        Product updatedProduct = await WithDbContextAsync(async context =>
            await context.Products.SingleAsync(product => product.Name == updateRequest.Name));

        (await client.GetAsync("/api/product/tax/2")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/product/item-discount/2")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/product/{updatedProduct.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        await WithDbContextAsync(async context =>
        {
            var deletedProduct = await context.Products.SingleAsync(product => product.Id == updatedProduct.Id);
            deletedProduct.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task ProductEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new ProductRequest
        {
            Name = string.Empty,
            Description = string.Empty,
            Price = -1,
            Stock = -1,
            ImageURL = "not-a-url"
        };

        (await client.PostAsJsonAsync("/api/product", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/product/99999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
