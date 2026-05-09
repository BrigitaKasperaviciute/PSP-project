using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class TaxControllerTests
{
    [Fact]
    public async Task CreateUpdateLinkDeleteAndReadTaxes_WorksEndToEnd()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "TaxRead,TaxWrite,ItemRead,ItemWrite");

        var productRequest = new POS_System.Business.Dtos.Request.ProductRequest
        {
            Name = $"TaxProd-{Guid.NewGuid():N}"[0..40],
            Description = "Tax link product",
            Price = 100,
            ImageURL = "http://example.com/product.png",
            Stock = 5
        };

        var productCreateResponse = await client.PostAsync("api/product", TestDataFactory.ToJsonContent(productRequest));
        var productCreateBody = await productCreateResponse.Content.ReadAsStringAsync();
        productCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK, productCreateBody);

        var productId = factory.UseDbContext(db => db.Products.Single(product => product.Name == productRequest.Name).Id);

        var createRequest = new TaxRequest { Name = $"Tax-{Guid.NewGuid():N}", Rate = 12, IsPercentage = true };
        var createResponse = await client.PostAsync("api/tax", TestDataFactory.ToJsonContent(createRequest));
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, createBody);
        createBody.Should().Contain(createRequest.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var createdTax = db.Taxes.Single(t => t.Name == createRequest.Name);

        var getAllResponse = await client.GetAsync("api/tax");
        var getAllBody = await getAllResponse.Content.ReadAsStringAsync();
        getAllResponse.StatusCode.Should().Be(HttpStatusCode.OK, getAllBody);
        getAllBody.Should().Contain(createRequest.Name);

        var getByIdResponse = await client.GetAsync($"api/tax/{createdTax.Id}");
        var getByIdBody = await getByIdResponse.Content.ReadAsStringAsync();
        getByIdResponse.StatusCode.Should().Be(HttpStatusCode.OK, getByIdBody);
        getByIdBody.Should().Contain(createRequest.Name);

        var updateRequest = new TaxRequest { Name = $"TaxUpd-{Guid.NewGuid():N}", Rate = 15, IsPercentage = false };
        var updateResponse = await client.PutAsync($"api/tax/{createdTax.Id}", TestDataFactory.ToJsonContent(updateRequest));
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);
        updateBody.Should().Contain(updateRequest.Name);

        var updatedTax = db.Taxes.Single(t => t.Name == updateRequest.Name);
        updatedTax.IsDeleted.Should().BeFalse();

        var linkResponse = await client.PutAsync($"api/tax/{updatedTax.Id}/link?itemsAreProducts=true", TestDataFactory.ToJsonContent(new[] { productId }));
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        factory.UseDbContext(db => db.ProductOnTaxes.Any(link => link.LeftEntityId == productId && link.RightEntityId == updatedTax.Id && link.EndDate == null).Should().BeTrue());

        var linkedResponse = await client.GetAsync($"api/tax/item/{productId}?isProduct=true");
        var linkedBody = await linkedResponse.Content.ReadAsStringAsync();
        linkedResponse.StatusCode.Should().Be(HttpStatusCode.OK, linkedBody);
        linkedBody.Should().Contain(updateRequest.Name);

        var unlinkResponse = await client.PutAsync($"api/tax/{updatedTax.Id}/unlink?itemsAreProducts=true", TestDataFactory.ToJsonContent(new[] { productId }));
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deletedResponse = await client.DeleteAsync($"api/tax/{updatedTax.Id}");
        deletedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        factory.UseDbContext(db => db.Taxes.Single(t => t.Id == updatedTax.Id).IsDeleted.Should().BeTrue());
    }

    [Fact]
    public async Task GetTaxById_NonexistentId_ReturnsNotFound()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "TaxRead");

        var response = await client.GetAsync("api/tax/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}