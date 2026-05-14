using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class BusinessDetailControllerIntegrationTests
{
    [Fact]
    public async Task CreateBusinessDetails_ValidPayloadPersistsBusinessDetailsAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "BusinessDetailsWrite", "BusinessDetailsRead" });

        // Arrange
        var payload = new BusinessDetailsRequest
        {
            BusinessName = "Integration Salon",
            BusinessEmail = "salon@example.com",
            BusinessPhone = "123456789",
            Country = "LT",
            City = "Vilnius",
            Street = "Main Street",
            HouseNumber = 10,
            FlatNumber = 2
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/business-details", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var createdDetails = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        createdDetails.Should().NotBeNull();
        createdDetails!.BusinessName.Should().Be(payload.BusinessName);
        createdDetails.BusinessEmail.Should().Be(payload.BusinessEmail);
    }

    [Fact]
    public async Task UpdateBusinessDetails_ExistingDetailsPersistsChangesAndReturnsResponse()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(new[] { "BusinessDetailsWrite", "BusinessDetailsRead" });

        // Arrange
        var payload = new BusinessDetailsRequest
        {
            BusinessName = "Updated Salon",
            BusinessEmail = "updated@example.com",
            BusinessPhone = "987654321",
            Country = "LT",
            City = "Kaunas",
            Street = "Second Street",
            HouseNumber = 15,
            FlatNumber = null
        };

        // Act
        var response = await client.PutAsJsonAsync("/api/business-details", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updatedDetails = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        updatedDetails.Should().NotBeNull();
        updatedDetails!.BusinessName.Should().Be(payload.BusinessName);
        updatedDetails.BusinessEmail.Should().Be(payload.BusinessEmail);

        var getResponse = await client.GetAsync("/api/business-details");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetchedDetails = await getResponse.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        fetchedDetails.Should().NotBeNull();
        fetchedDetails!.BusinessName.Should().Be(payload.BusinessName);
    }
}