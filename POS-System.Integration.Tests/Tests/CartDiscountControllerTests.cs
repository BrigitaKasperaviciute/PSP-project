using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using POS_System.Integration.Tests.Helpers;

namespace POS_System.Integration.Tests.Tests;

public class CartDiscountControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetById_NonExistingDiscount_ReturnsNotFound()
    {
        var response = await _client.GetAsync("/api/cart-discount/nonexistent-id");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteById_NonExistingDiscount_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync("/api/cart-discount/nonexistent-id");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReachesController()
    {
        var request = new { Value = 10, IsPercentage = true, EndDate = (DateTime?)null };

        var response = await _client.PostAsJsonAsync("/api/cart-discount", request);

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }
}
