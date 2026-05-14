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
public sealed class ItemDiscountControllerTests : IntegrationTestBase
{
    public ItemDiscountControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task ItemDiscountScenario_covers_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/item-discount?pageNum=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new ItemDiscountRequest
        {
            Value = 15,
            IsPercentage = true,
            Description = "Integration item discount",
            StartDate = null,
            EndDate = null
        };

        (await client.PostAsJsonAsync("/api/item-discount", createRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        ItemDiscount createdDiscount = await WithDbContextAsync(async context =>
            await context.ItemDiscounts.SingleAsync(discount => discount.Description == createRequest.Description));

        (await client.GetAsync($"/api/item-discount/{createdDiscount.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRequest = new ItemDiscountRequest
        {
            Value = 20,
            IsPercentage = true,
            Description = "Updated integration item discount",
            StartDate = null,
            EndDate = null
        };

        (await client.PutAsJsonAsync($"/api/item-discount/{createdDiscount.Id}", updateRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        ItemDiscount updatedDiscount = await WithDbContextAsync(async context =>
            await context.ItemDiscounts.SingleAsync(discount => discount.Description == updateRequest.Description));

        (await client.PutAsJsonAsync($"/api/item-discount/{updatedDiscount.Id}/link?itemsAreProducts=true", new[] { 4 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/item-discount/item/4?isProduct=true")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/item-discount/{updatedDiscount.Id}/unlink?itemsAreProducts=true", new[] { 4 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/item-discount/{updatedDiscount.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ItemDiscountEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new ItemDiscountRequest
        {
            Value = 0,
            IsPercentage = true,
            Description = string.Empty,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(-2)
        };

        (await client.PostAsJsonAsync("/api/item-discount", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/item-discount/99999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
