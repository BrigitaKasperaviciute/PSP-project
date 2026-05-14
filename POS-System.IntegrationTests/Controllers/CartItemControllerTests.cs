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
public sealed class CartItemControllerTests : IntegrationTestBase
{
    public CartItemControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CartItemScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/carts/1/items?pageNum=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/carts/1/items/1")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new CartItemRequest
        {
            CartId = 1,
            Quantity = 2,
            IsProduct = true,
            ProductVersionId = 4,
            ServiceVersionId = null
        };

        (await client.PostAsJsonAsync("/api/carts/1/items", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        CartItem createdCartItem = await WithDbContextAsync(async context =>
            await context.CartItems.SingleAsync(cartItem => cartItem.CartId == createRequest.CartId && cartItem.Quantity == createRequest.Quantity && cartItem.ProductVersionId == createRequest.ProductVersionId));

        var updateRequest = new CartItemRequest
        {
            CartId = 1,
            Quantity = 3,
            IsProduct = true,
            ProductVersionId = 4,
            ServiceVersionId = null
        };

        (await client.PutAsJsonAsync($"/api/carts/1/items/{createdCartItem.Id}", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/carts/1/items/{createdCartItem.Id}/link", new[] { 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/carts/1/items/{createdCartItem.Id}/unlink", new[] { 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/carts/1/items/{createdCartItem.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CartItemEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new CartItemRequest
        {
            CartId = 1,
            Quantity = 0,
            IsProduct = true,
            ProductVersionId = null,
            ServiceVersionId = 1
        };

        (await client.PostAsJsonAsync("/api/carts/1/items", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/carts/1/items/99999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
