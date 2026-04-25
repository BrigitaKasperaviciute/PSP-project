using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.Integration.Tests.Infrastructure;

namespace POS_System.Integration.Tests.Controllers;

public class BusinessDetailControllerTests : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _authClient;
    private readonly HttpClient _anonClient;

    public BusinessDetailControllerTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _authClient = factory.CreateClient();
        TestAuthHelper.AddFullAccessAuth(_authClient);
        _anonClient = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.EnsureInitializedAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static BusinessDetailsRequest ValidRequest(string name = "Test Business") => new()
    {
        BusinessName = name,
        BusinessEmail = "biz@test.com",
        BusinessPhone = "555000111",
        Country = "Lithuania",
        City = "Vilnius",
        Street = "Test Street",
        HouseNumber = 2,
        FlatNumber = null
    };

    [Fact]
    public async Task Create_ValidRequest_ReturnsOkWithBusinessDetails()
    {
        // Arrange
        var request = ValidRequest("CreateTestBusiness");

        // Act
        var response = await _authClient.PostAsJsonAsync("/api/business-details", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be("CreateTestBusiness");
        body.City.Should().Be("Vilnius");
    }

    [Fact]
    public async Task Create_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var response = await _anonClient.PostAsJsonAsync("/api/business-details", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsOkWithUpdatedDetails()
    {
        // Arrange – ensure business details exist first
        await _authClient.PostAsJsonAsync("/api/business-details", ValidRequest("BeforeUpdate"));
        var updateRequest = ValidRequest("AfterUpdate");

        // Act
        var response = await _authClient.PutAsJsonAsync("/api/business-details", updateRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().Be("AfterUpdate");
    }

    [Fact]
    public async Task Update_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var response = await _anonClient.PutAsJsonAsync("/api/business-details", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_AfterCreation_ReturnsOkWithDetails()
    {
        // Arrange – create business details first
        var createRequest = ValidRequest("GetTestBusiness");
        await _authClient.PostAsJsonAsync("/api/business-details", createRequest);

        // Act
        var response = await _authClient.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<BusinessDetailsResponse>();
        body.Should().NotBeNull();
        body!.BusinessName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Get_MissingToken_ReturnsUnauthorized()
    {
        // Arrange
        // (no token)

        // Act
        var response = await _anonClient.GetAsync("/api/business-details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
