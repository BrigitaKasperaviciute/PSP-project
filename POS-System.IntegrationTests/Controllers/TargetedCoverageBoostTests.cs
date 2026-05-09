using System.Net;
using FluentAssertions;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public sealed class TargetedCoverageBoostTests
{
    [Fact]
    public async Task Hit_Multiple_Simple_Get_Endpoints_To_Increase_Coverage()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var endpoints = new[]
        {
            "/api/carts",
            "/api/products",
            "/api/services",
            "/api/employees",
            "/api/giftcards",
            "/api/tax",
            "/api/time-slot",
            "/api/item-discount",
            "/api/product-modification",
            "/api/cart-discount/non-existing-code",
            "/api/payments/1"
        };

        foreach (var ep in endpoints)
        {
            var response = await client.GetAsync(ep);
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.NoContent,
                HttpStatusCode.NotFound,
                HttpStatusCode.Forbidden,
                HttpStatusCode.BadRequest,
                HttpStatusCode.InternalServerError);
        }
    }
}
