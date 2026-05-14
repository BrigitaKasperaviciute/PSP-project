using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class CartControllerIntegrationTests
{
    [Fact]
    public async Task GetAll_ReturnsPagedCarts()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResponse<CartResponse>>();
        body.Should().NotBeNull();
        body!.TotalCount.Should().BeGreaterThan(0);
        body.Results.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetById_ExistingCart_ReturnsCart()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartResponse>();
        body.Should().NotBeNull();
        body!.Id.Should().Be(1);
    }

    [Fact]
    public async Task Create_ValidPayload_PersistsCartAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Carts.CountAsync());
        var payload = new { employeeVersionId = 1 };

        // Act
        var response = await client.PostAsJsonAsync("/api/carts", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        json.RootElement.GetProperty("employeeVersionId").GetInt32().Should().Be(1);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Carts.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task Create_InvalidJson_ReturnsBadRequestAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Carts.CountAsync());

        // Act
        var response = await IntegrationTestHelpers.PostMalformedJsonAsync(client, "/api/carts");

        // Assert
        await IntegrationTestHelpers.AssertBadRequestAsync(response);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Carts.CountAsync());
        countAfter.Should().Be(countBefore);
    }

    [Fact]
    public async Task Delete_CreatedInProgressCart_RemovesRecord()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var created = await (await client.PostAsJsonAsync("/api/carts", new { employeeVersionId = 1 }))
            .Content.ReadFromJsonAsync<CartResponse>();
        created.Should().NotBeNull();

        // Act
        var response = await client.DeleteAsync($"/api/carts/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var countAfter = await factory.ExecuteDbContextAsync(db => db.Carts.CountAsync(c => c.Id == created.Id));
        countAfter.Should().Be(0);
    }

    [Fact]
    public async Task GetCartDiscount_WithoutDiscount_ReturnsNullBody()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/carts/1/discount");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
