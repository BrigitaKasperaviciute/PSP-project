using System.Net;
using FluentAssertions;
using System;
using Microsoft.Extensions.DependencyInjection;
using POS_System.IntegrationTests.TestSupport;
using POS_System.IntegrationTests.Helpers;
using POS_System.Business.Dtos.Request;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class CartControllerTests
{
    [Fact]
    public async Task CreateCart_ValidRequest_ReturnsCartAndPersists()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var request = new CartRequest { EmployeeVersionId = 0 };

        // Act
        var response = await client.PostAsync("api/carts", TestDataFactory.ToJsonContent(request));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.IndexOf("employeeVersionId", StringComparison.OrdinalIgnoreCase).Should().BeGreaterThan(-1);

        // Validate DB state
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var cart = db.Carts.OrderByDescending(c => c.Id).FirstOrDefault();
        cart.Should().NotBeNull();
        cart!.EmployeeVersionId.Should().Be(0);
    }

    [Fact]
    public async Task ApplyDiscount_NonexistentCoupon_ReturnsNotFound()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        // Create a cart to operate on
        var cartRequest = new CartRequest { EmployeeVersionId = 0 };
        var createResp = await client.PostAsync("api/carts", TestDataFactory.ToJsonContent(cartRequest));
        var createBody = await createResp.Content.ReadAsStringAsync();
        createResp.StatusCode.Should().Be(HttpStatusCode.OK, createBody);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var cart = db.Carts.OrderByDescending(c => c.Id).First();

        var applyRequest = new { DiscountCode = "NON_EXISTENT_COUPON" };

        // Act
        var response = await client.PatchAsync($"api/carts/{cart.Id}/discount", TestDataFactory.ToJsonContent(applyRequest));

        // Assert - coupon service will return null -> NotFound
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
