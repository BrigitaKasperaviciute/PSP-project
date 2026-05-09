using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ServiceControllerTests
{
    [Fact]
    public async Task CreateUpdateDeleteAndLinkServices_WorksEndToEnd()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ServiceRead,ServiceWrite,ItemRead,TaxWrite,ItemDiscountWrite");

        var createRequest = new ServiceRequest
        {
            Name = $"Service-{Guid.NewGuid():N}",
            Description = "Integration test service",
            Duration = 30,
            Price = 2500,
            ImageURL = "http://example.com/service.png",
            EmployeeId = 1
        };

        var createResponse = await client.PostAsync("api/services", TestDataFactory.ToJsonContent(createRequest));
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, createBody);
        createBody.Should().Contain(createRequest.Name);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var createdService = db.Services.Single(service => service.Name == createRequest.Name);

        var listResponse = await client.GetAsync("api/services?pageNum=0&pageSize=10");
        var listBody = await listResponse.Content.ReadAsStringAsync();
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK, listBody);
        listBody.Should().Contain(createRequest.Name);

        var getResponse = await client.GetAsync($"api/services/{createdService.Id}");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, getBody);
        getBody.Should().Contain(createRequest.Name);

        var updateRequest = new ServiceRequest
        {
            Name = $"Svc-{Guid.NewGuid():N}",
            Description = "Updated integration test service",
            Duration = 45,
            Price = 3000,
            ImageURL = "http://example.com/service2.png",
            EmployeeId = 1
        };

        var updateResponse = await client.PutAsync($"api/services/{createdService.Id}", TestDataFactory.ToJsonContent(updateRequest));
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);
        updateBody.Should().Contain(updateRequest.Name);

        var updatedService = db.Services.Single(service => service.Name == updateRequest.Name);

        var taxLinkResponse = await client.PutAsync($"api/tax/2/link?itemsAreProducts=false", TestDataFactory.ToJsonContent(new[] { updatedService.Id }));
        taxLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var linkedByTaxResponse = await client.GetAsync("api/services/tax/2");
        var linkedByTaxBody = await linkedByTaxResponse.Content.ReadAsStringAsync();
        linkedByTaxResponse.StatusCode.Should().Be(HttpStatusCode.OK, linkedByTaxBody);
        linkedByTaxBody.Should().Contain(updateRequest.Name);

        var discountCreateRequest = new ItemDiscountRequest
        {
            Value = 25,
            IsPercentage = true,
            Description = $"Service link discount {Guid.NewGuid():N}",
            StartDate = null,
            EndDate = null
        };

        var discountCreateResponse = await client.PostAsync("api/item-discount", TestDataFactory.ToJsonContent(discountCreateRequest));
        discountCreateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdDiscount = db.ItemDiscounts.Single(discount => discount.Description == discountCreateRequest.Description);

        var discountLinkResponse = await client.PutAsync($"api/item-discount/{createdDiscount.Id}/link?itemsAreProducts=false", TestDataFactory.ToJsonContent(new[] { updatedService.Id }));
        discountLinkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var linkedByDiscountResponse = await client.GetAsync($"api/services/item-discount/{createdDiscount.Id}");
        var linkedByDiscountBody = await linkedByDiscountResponse.Content.ReadAsStringAsync();
        linkedByDiscountResponse.StatusCode.Should().Be(HttpStatusCode.OK, linkedByDiscountBody);
        linkedByDiscountBody.Should().Contain(updateRequest.Name);

        var deleteResponse = await client.DeleteAsync("api/services/2");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        db.Services.Single(service => service.Id == 2).IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetServiceById_NonexistentId_ReturnsNotFound()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ServiceRead");

        var response = await client.GetAsync("api/services/999999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}