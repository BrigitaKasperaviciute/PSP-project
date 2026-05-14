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
public sealed class ServiceControllerTests : IntegrationTestBase
{
    public ServiceControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ServiceScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/services?pageNum=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/services/1")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new ServiceRequest
        {
            Name = "Integration Service",
            Description = "Integration service description",
            Duration = 35,
            Price = 149,
            ImageURL = "https://example.com/service.png",
            EmployeeId = 1
        };

        (await client.PostAsJsonAsync("/api/services", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        Service createdService = await WithDbContextAsync(async context =>
            await context.Services.SingleAsync(service => service.Name == createRequest.Name));

        var updateRequest = new ServiceRequest
        {
            Name = "Integration Service Updated",
            Description = "Updated integration service description",
            Duration = 40,
            Price = 199,
            ImageURL = "https://example.com/service-updated.png",
            EmployeeId = 1
        };

        (await client.PutAsJsonAsync($"/api/services/{createdService.Id}", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        Service updatedService = await WithDbContextAsync(async context =>
            await context.Services.SingleAsync(service => service.Name == updateRequest.Name));

        (await client.GetAsync("/api/services/tax/2")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/services/item-discount/2")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/services/{updatedService.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await WithDbContextAsync(async context =>
        {
            var deletedService = await context.Services.SingleAsync(service => service.Id == updatedService.Id);
            deletedService.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task ServiceEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new ServiceRequest
        {
            Name = string.Empty,
            Description = string.Empty,
            Duration = 0,
            Price = -1,
            ImageURL = "not-a-url",
            EmployeeId = 1
        };

        (await client.PostAsJsonAsync("/api/services", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/services/99999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
