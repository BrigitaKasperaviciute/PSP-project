using System.Net;
using FluentAssertions;
using System;
using Microsoft.Extensions.DependencyInjection;
using POS_System.IntegrationTests.TestSupport;
using POS_System.IntegrationTests.Helpers;
using POS_System.Business.Dtos.Request;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class GiftCardControllerTests
{
    [Fact]
    public async Task CreateGiftCard_ValidRequest_ReturnsCreatedAndPersists()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "GiftCardWrite");

        var request = new GiftCardRequest { Date = DateOnly.FromDateTime(DateTime.UtcNow), Value = 50 };

        // Act
        var response = await client.PostAsync("api/giftcards", TestDataFactory.ToJsonContent(request));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.IndexOf("value", StringComparison.OrdinalIgnoreCase).Should().BeGreaterThan(-1);

        // Validate DB
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<POS_System.Data.Database.ApplicationDbContext>();
        var gift = db.GiftCards.SingleOrDefault(g => g.Value == 50);
        gift.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateGiftCard_NullBody_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Claims", "GiftCardWrite");

        // Act
        var response = await client.PostAsync("api/giftcards", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, body);
    }
}
