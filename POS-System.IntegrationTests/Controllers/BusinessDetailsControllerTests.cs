using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection("Integration tests")]
public sealed class BusinessDetailsControllerTests : IntegrationTestBase
{
    public BusinessDetailsControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task BusinessDetailsScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);
        var initialResponse = await client.GetAsync("/api/business-details");
        initialResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new BusinessDetailsRequest
        {
            BusinessName = "Integration Business",
            BusinessEmail = "business@example.com",
            BusinessPhone = "+12345678901",
            Country = "Country",
            City = "City",
            Street = "Street",
            HouseNumber = 10,
            FlatNumber = 2
        };

        (await client.PostAsJsonAsync("/api/business-details", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRequest = new BusinessDetailsRequest
        {
            BusinessName = "Integration Business Updated",
            BusinessEmail = "updated@example.com",
            BusinessPhone = "+12345678902",
            Country = "Country",
            City = "City",
            Street = "Street",
            HouseNumber = 12,
            FlatNumber = null
        };

        (await client.PutAsJsonAsync("/api/business-details", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var updatedResponse = await client.GetAsync("/api/business-details");
        updatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await updatedResponse.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        payload.Should().NotBeNull();
        payload!.BusinessName.Should().Be(updateRequest.BusinessName);
        payload.BusinessEmail.Should().Be(updateRequest.BusinessEmail);
    }

    [Fact]
    public async Task BusinessDetailsEndpoints_return_validation_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new BusinessDetailsRequest
        {
            BusinessName = "Integration Business",
            BusinessEmail = "not-an-email",
            BusinessPhone = "+12345678901",
            Country = "Country",
            City = "City",
            Street = "Street",
            HouseNumber = 10,
            FlatNumber = 2
        };

        (await client.PostAsJsonAsync("/api/business-details", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
