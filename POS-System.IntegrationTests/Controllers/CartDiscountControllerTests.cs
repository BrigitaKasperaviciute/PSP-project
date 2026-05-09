using System.Net;
using System.Text.Json;
using FluentAssertions;
using POS_System.Business.Dtos.Response;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class CartDiscountControllerTests
{
    [Fact]
    public async Task GetCartDiscountById_ExistingSeededDiscount_ReturnsDiscount()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var discountId = Guid.NewGuid().ToString("N");
        factory.UseDbContext(db =>
        {
            db.Set<CartDiscount>().Add(new CartDiscount
            {
                Id = discountId,
                Value = 15,
                IsPercentage = true
            });
            db.SaveChanges();
        });

        // Act
        var response = await client.GetAsync($"api/cart-discount/{discountId}");
        var body = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<CartDiscountResponse>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(discountId);
        payload.Value.Should().Be(15);
        payload.IsPercentage.Should().BeTrue();

        // Validate DB state
        factory.UseDbContext(db => db.Set<CartDiscount>().Single(discount => discount.Id == discountId).Value.Should().Be(15));
    }

    [Fact]
    public async Task GetCartDiscountById_MissingDiscount_ReturnsNotFound()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("api/cart-discount/missing-discount");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, body);
        body.Should().ContainEquivalentOf("not found");
    }
}