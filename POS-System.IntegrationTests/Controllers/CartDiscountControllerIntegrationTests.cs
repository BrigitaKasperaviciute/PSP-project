using System.Net;
using FluentAssertions;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class CartDiscountControllerIntegrationTests
{
    [Fact]
    public async Task GetCartDiscountById_MissingDiscountReturnsNotFound()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(Array.Empty<string>());

        var response = await client.GetAsync($"/api/cart-discount/{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteCartDiscountById_MissingDiscountReturnsNotFound()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient(Array.Empty<string>());

        var response = await client.DeleteAsync($"/api/cart-discount/{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}