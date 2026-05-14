using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection("Integration tests")]
public sealed class CartControllerTests : IntegrationTestBase
{
    public CartControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CartScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient();

        (await client.GetAsync("/api/carts?pageNum=0&pageSize=35")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/carts/1")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new CartRequest
        {
            EmployeeVersionId = 1
        };

        (await client.PostAsJsonAsync("/api/carts", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var discountResponse = await client.GetAsync("/api/carts/1/discount");
        discountResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await discountResponse.Content.ReadAsStringAsync()).Should().BeEmpty();

        (await client.DeleteAsync("/api/carts/3")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CartDelete_endpoint_returns_error_for_non_in_progress_cart()
    {
        await ResetDatabaseAsync();

        var client = CreateClient();

        (await client.DeleteAsync("/api/carts/2")).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
