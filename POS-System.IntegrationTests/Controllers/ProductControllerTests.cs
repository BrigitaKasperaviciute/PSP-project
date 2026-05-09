using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.IntegrationTests.TestSupport;
using POS_System.IntegrationTests.Helpers;
using POS_System.Business.Dtos.Request;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class ProductControllerTests
{
    [Fact]
    public async Task CreateProduct_ValidRequest_ReturnsCreatedAndPersists()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        // Add write permission via header so authorization succeeds
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ItemWrite");

        var request = new ProductRequest
        {
            Name = "Test Product",
            Description = "A product",
            Price = 100,
            ImageURL = "http://example.com/img.png",
            Stock = 10
        };

        // Act
        var response = await client.PostAsync("api/product", TestDataFactory.ToJsonContent(request));

        // Read body for diagnostics
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain(request.Name);
        body.Should().Contain("100");

        // Validate DB state
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var product = db.Products.SingleOrDefault(p => p.Name == "Test Product");
        product.Should().NotBeNull();
        product!.Price.Should().Be(100);
    }

    [Fact]
    public async Task CreateProduct_NullBody_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "ItemWrite");

        // Act
        var response = await client.PostAsync("api/product", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
    }
}
