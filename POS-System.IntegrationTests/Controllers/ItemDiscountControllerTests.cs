using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ItemDiscountControllerTests
{
    [Fact]
    public async Task CreateUpdateLinkDeleteAndReadItemDiscounts_WorksEndToEnd()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ItemDiscountRead,ItemDiscountWrite,ItemRead,ItemWrite");

        var productRequest = new POS_System.Business.Dtos.Request.ProductRequest
        {
            Name = $"DiscProd-{Guid.NewGuid():N}"[0..40],
            Description = "Discount link product",
            Price = 150,
            ImageURL = "http://example.com/discount-product.png",
            Stock = 5
        };

        var productCreateResponse = await client.PostAsync("api/product", TestDataFactory.ToJsonContent(productRequest));
        var productCreateBody = await productCreateResponse.Content.ReadAsStringAsync();
        productCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK, productCreateBody);

        var productId = factory.UseDbContext(db => db.Products.Single(product => product.Name == productRequest.Name).Id);

        var createRequest = new ItemDiscountRequest
        {
            Value = 20,
            IsPercentage = true,
            Description = $"Discount-{Guid.NewGuid():N}",
            StartDate = null,
            EndDate = null
        };

        var createResponse = await client.PostAsync("api/item-discount", TestDataFactory.ToJsonContent(createRequest));
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, createBody);
        createBody.Should().Contain(createRequest.Description);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var createdDiscount = db.ItemDiscounts.Single(discount => discount.Description == createRequest.Description);

        var listResponse = await client.GetAsync("api/item-discount?pageNum=0&pageSize=35");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        listBody.Should().Contain(createRequest.Description);

        var getResponse = await client.GetAsync($"api/item-discount/{createdDiscount.Id}");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, getBody);
        getBody.Should().Contain(createRequest.Description);

        var updateRequest = new ItemDiscountRequest
        {
            Value = 30,
            IsPercentage = false,
            Description = $"DiscountUpd-{Guid.NewGuid():N}",
            StartDate = null,
            EndDate = null
        };

        var updateResponse = await client.PutAsync($"api/item-discount/{createdDiscount.Id}", TestDataFactory.ToJsonContent(updateRequest));
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);
        updateBody.Should().Contain(updateRequest.Description);

        var updatedDiscount = db.ItemDiscounts.Single(discount => discount.Description == updateRequest.Description);

        var linkResponse = await client.PutAsync($"api/item-discount/{updatedDiscount.Id}/link?itemsAreProducts=true", TestDataFactory.ToJsonContent(new[] { productId }));
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        factory.UseDbContext(db => db.ProductOnItemDiscounts.Any(link => link.LeftEntityId == productId && link.RightEntityId == updatedDiscount.Id && link.EndDate == null).Should().BeTrue());

        var linkedResponse = await client.GetAsync($"api/item-discount/item/{productId}?isProduct=true");
        var linkedBody = await linkedResponse.Content.ReadAsStringAsync();
        linkedResponse.StatusCode.Should().Be(HttpStatusCode.OK, linkedBody);
        linkedBody.Should().Contain(updateRequest.Description);

        var unlinkResponse = await client.PutAsync($"api/item-discount/{updatedDiscount.Id}/unlink?itemsAreProducts=true", TestDataFactory.ToJsonContent(new[] { productId }));
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync($"api/item-discount/{updatedDiscount.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.UseDbContext(db => db.ItemDiscounts.Single(discount => discount.Id == updatedDiscount.Id).IsDeleted.Should().BeTrue());
    }

    [Fact]
    public async Task GetItemDiscountById_NonexistentId_ReturnsNotFound()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ItemDiscountRead");

        var response = await client.GetAsync("api/item-discount/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}