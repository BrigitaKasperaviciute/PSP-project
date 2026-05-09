using System.Net;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.Helpers;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class BusinessDetailControllerTests
{
    [Fact]
    public async Task CreateUpdateAndReadBusinessDetails_PersistsToFile()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "BusinessDetailsRead,BusinessDetailsWrite");

        var initialRequest = new BusinessDetailsRequest
        {
            BusinessName = "Store One",
            BusinessEmail = "store1@example.com",
            BusinessPhone = "111111111",
            Country = "Country A",
            City = "City A",
            Street = "Street A",
            HouseNumber = 10,
            FlatNumber = 2
        };

        var createResponse = await client.PostAsync("api/business-details", TestDataFactory.ToJsonContent(initialRequest));
        var createBody = await createResponse.Content.ReadAsStringAsync();
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK, createBody);
        createBody.Should().Contain(initialRequest.BusinessName);
        File.ReadAllText(factory.BusinessDetailsPath).Should().Contain(initialRequest.BusinessName);

        var updateRequest = new BusinessDetailsRequest
        {
            BusinessName = "Store Two",
            BusinessEmail = "store2@example.com",
            BusinessPhone = "222222222",
            Country = "Country B",
            City = "City B",
            Street = "Street B",
            HouseNumber = 20,
            FlatNumber = null
        };

        var updateResponse = await client.PutAsync("api/business-details", TestDataFactory.ToJsonContent(updateRequest));
        var updateBody = await updateResponse.Content.ReadAsStringAsync();
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK, updateBody);
        updateBody.Should().Contain(updateRequest.BusinessName);
        File.ReadAllText(factory.BusinessDetailsPath).Should().Contain(updateRequest.BusinessName);

        var getResponse = await client.GetAsync("api/business-details");
        var getBody = await getResponse.Content.ReadAsStringAsync();
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK, getBody);
        getBody.Should().Contain(updateRequest.BusinessName);
    }

    [Fact]
    public async Task CreateBusinessDetails_NullBody_ReturnsBadRequest()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "BusinessDetailsWrite");

        var response = await client.PostAsync("api/business-details", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
    }
}