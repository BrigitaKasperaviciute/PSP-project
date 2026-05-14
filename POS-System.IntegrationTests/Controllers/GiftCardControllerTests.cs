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
public sealed class GiftCardControllerTests : IntegrationTestBase
{
    public GiftCardControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task GiftCardScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/giftcards?pageNum=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            Value = 250
        };

        (await client.PostAsJsonAsync("/api/giftcards", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        const string giftCardId = "87654321";

        await WithDbContextAsync(async context =>
        {
            context.GiftCards.Add(new GiftCard
            {
                Id = giftCardId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                Value = 250
            });

            await context.SaveChangesAsync();
        });

        (await client.GetAsync($"/api/giftcards/{giftCardId}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRequest = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            Value = 500
        };

        (await client.PutAsJsonAsync($"/api/giftcards/{giftCardId}", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/giftcards/{giftCardId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GiftCardEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new GiftCardRequest
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Value = 0
        };

        (await client.PostAsJsonAsync("/api/giftcards", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/giftcards/does-not-exist")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
