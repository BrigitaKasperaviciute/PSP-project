using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class CartItemControllerTests
{
    [Fact]
    public async Task CreateUpdateLinkDeleteAndReadCartItems_WorksEndToEnd()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "CartItemRead,CartItemWrite,ItemRead,ItemWrite");

        var productModificationRequest = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name = $"CI-{Guid.NewGuid():N}"[0..35],
            Description = "Cart item modification",
            Price = 33
        };

        var productModificationCreateResponse = await client.PostAsync("api/product-modification", TestDataFactory.ToJsonContent(productModificationRequest));
        var productModificationCreateBody = await productModificationCreateResponse.Content.ReadAsStringAsync();
        productModificationCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK, productModificationCreateBody);

        var productModificationId = factory.UseDbContext(db => db.ProductModifications.Single(modification => modification.Name == productModificationRequest.Name).Id);

        var createRequest = new CartItemRequest
        {
            CartId = 1,
            Quantity = 77,
            IsProduct = true,
            ProductVersionId = 1,
            ServiceVersionId = null
        };

        var createResponse = await client.PostAsync("api/carts/1/items", TestDataFactory.ToJsonContent(createRequest));
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, createBody);
        createBody.Should().Contain("77");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var createdItem = db.CartItems.Single(item => item.CartId == 1 && item.Quantity == 77);

        var getAllResponse = await client.GetAsync("api/carts/1/items?pageNum=0&pageSize=10");
        var getAllBody = await getAllResponse.Content.ReadAsStringAsync();
        getAllResponse.StatusCode.Should().Be(HttpStatusCode.OK, getAllBody);
        getAllBody.Should().Contain("77");

        var getByIdResponse = await client.GetAsync($"api/carts/1/items/{createdItem.Id}");
        var getByIdBody = await getByIdResponse.Content.ReadAsStringAsync();
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.OK, getByIdBody);
        getByIdBody.Should().Contain("77");

        var updateRequest = new CartItemRequest
        {
            CartId = 1,
            Quantity = 88,
            IsProduct = true,
            ProductVersionId = 1,
            ServiceVersionId = null
        };

        var updateResponse = await client.PutAsync($"api/carts/1/items/{createdItem.Id}", TestDataFactory.ToJsonContent(updateRequest));
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);
        updateBody.Should().Contain("88");

        var linkedResponse = await client.PutAsync($"api/carts/1/items/{createdItem.Id}/link", TestDataFactory.ToJsonContent(new[] { productModificationId }));
        linkedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.UseDbContext(db => db.ProductModificationOnCartItems.Any(link => link.LeftEntityId == productModificationId && link.RightEntityId == createdItem.Id && link.EndDate == null).Should().BeTrue());

        var linkedModsResponse = await client.GetAsync($"api/product-modification/cart-item/{createdItem.Id}");
        var linkedModsBody = await linkedModsResponse.Content.ReadAsStringAsync();
        linkedModsResponse.StatusCode.Should().Be(HttpStatusCode.OK, linkedModsBody);
        linkedModsBody.Should().Contain(productModificationRequest.Name);

        var unlinkResponse = await client.PutAsync($"api/carts/1/items/{createdItem.Id}/unlink", TestDataFactory.ToJsonContent(new[] { productModificationId }));
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.UseDbContext(db => db.ProductModificationOnCartItems.Any(link => link.LeftEntityId == productModificationId && link.RightEntityId == createdItem.Id && link.EndDate == null).Should().BeFalse());

        var deleteResponse = await client.DeleteAsync($"api/carts/1/items/{createdItem.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        db.CartItems.Any(item => item.Id == createdItem.Id).Should().BeFalse();
    }

    [Fact]
    public async Task CreateCartItem_InvalidCart_ReturnsBadRequest()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "CartItemWrite");

        var request = new CartItemRequest
        {
            CartId = 999999,
            Quantity = 1,
            IsProduct = true,
            ProductVersionId = 1,
            ServiceVersionId = null
        };

        var response = await client.PostAsync("api/carts/999999/items", TestDataFactory.ToJsonContent(request));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}