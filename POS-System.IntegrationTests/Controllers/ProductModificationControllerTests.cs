using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ProductModificationControllerTests
{
    [Fact]
    public async Task CreateUpdateLinkDeleteAndReadProductModifications_WorksEndToEnd()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ItemRead,ItemWrite,CartItemWrite");

        var createRequest = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name = $"Mod-{Guid.NewGuid():N}",
            Description = "Integration test modification",
            Price = 99
        };

        var createResponse = await client.PostAsync("api/product-modification", TestDataFactory.ToJsonContent(createRequest));
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, createBody);
        createBody.Should().Contain(createRequest.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var createdModification = db.ProductModifications.Single(modification => modification.Name == createRequest.Name);

        var listResponse = await client.GetAsync("api/product-modification?pageSize=10&pageNumber=0");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        listBody.Should().Contain(createRequest.Name);

        var getResponse = await client.GetAsync($"api/product-modification/{createdModification.Id}");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, getBody);
        getBody.Should().Contain(createRequest.Name);

        var versionsResponse = await client.GetAsync("api/product-modification/1/versions");
        var versionsBody = await versionsResponse.Content.ReadAsStringAsync();
        versionsResponse.StatusCode.Should().Be(HttpStatusCode.OK, versionsBody);
        versionsBody.Should().Contain("Extra cheese");

        var updateRequest = new ProductModificationRequest
        {
            ProductVersionId = 1,
            Name = $"ModUpd-{Guid.NewGuid():N}",
            Description = "Updated integration test modification",
            Price = 111
        };

        var updateResponse = await client.PutAsync($"api/product-modification/{createdModification.Id}", TestDataFactory.ToJsonContent(updateRequest));
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);
        updateBody.Should().Contain(updateRequest.Name);

        var updatedModification = db.ProductModifications.Single(modification => modification.Name == updateRequest.Name);

        var linkResponse = await client.PutAsync("api/carts/1/items/1/link", TestDataFactory.ToJsonContent(new[] { updatedModification.Id }));
        linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var linkedByCartItemResponse = await client.GetAsync("api/product-modification/cart-item/1");
        var linkedByCartItemBody = await linkedByCartItemResponse.Content.ReadAsStringAsync();
        linkedByCartItemResponse.StatusCode.Should().Be(HttpStatusCode.OK, linkedByCartItemBody);
        linkedByCartItemBody.Should().Contain(updateRequest.Name);

        var linkedByProductResponse = await client.GetAsync("api/product-modification/product/1?pageSize=10&pageNumber=0");
        var linkedByProductBody = await linkedByProductResponse.Content.ReadAsStringAsync();
        linkedByProductResponse.StatusCode.Should().Be(HttpStatusCode.OK, linkedByProductBody);
        linkedByProductBody.Should().Contain(updateRequest.Name);

        var unlinkResponse = await client.PutAsync("api/carts/1/items/1/unlink", TestDataFactory.ToJsonContent(new[] { updatedModification.Id }));
        unlinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var deleteResponse = await client.DeleteAsync($"api/product-modification/{updatedModification.Id}");
        var deleteBody = await deleteResponse.Content.ReadAsStringAsync();
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.OK, deleteBody);
        factory.UseDbContext(db => db.ProductModifications.Single(modification => modification.Id == updatedModification.Id).IsDeleted.Should().BeTrue());
    }

    [Fact]
    public async Task GetProductModificationById_NonexistentId_ReturnsNotFound()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ItemRead");

        var response = await client.GetAsync("api/product-modification/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}