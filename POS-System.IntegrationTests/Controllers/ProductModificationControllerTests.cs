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
public sealed class ProductModificationControllerTests : IntegrationTestBase
{
    public ProductModificationControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ProductModificationScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/product-modification?pageNumber=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/product-modification/1")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/product-modification/1/versions/")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new ProductModificationRequest
        {
            ProductVersionId = 4,
            Name = "Integration Modification",
            Description = "Integration modification description",
            Price = 55
        };

        (await client.PostAsJsonAsync("/api/product-modification", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        ProductModification createdModification = await WithDbContextAsync(async context =>
            await context.ProductModifications.SingleAsync(modification => modification.Name == createRequest.Name));

        var updateRequest = new ProductModificationRequest
        {
            ProductVersionId = 4,
            Name = "Integration Modification Updated",
            Description = "Updated integration modification description",
            Price = 65
        };

        (await client.PutAsJsonAsync($"/api/product-modification/{createdModification.Id}", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        ProductModification updatedModification = await WithDbContextAsync(async context =>
            await context.ProductModifications.SingleAsync(modification => modification.Name == updateRequest.Name));

        (await client.GetAsync("/api/product-modification/cart-item/1")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/product-modification/product/4")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/product-modification/{updatedModification.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ProductModificationEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new ProductModificationRequest
        {
            ProductVersionId = 4,
            Name = string.Empty,
            Description = string.Empty,
            Price = -1
        };

        (await client.PostAsJsonAsync("/api/product-modification", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/product-modification/99999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
