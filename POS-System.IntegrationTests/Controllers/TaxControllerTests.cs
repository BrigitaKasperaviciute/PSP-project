using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Data.Database;
using POS_System.Domain.Entities;
using POS_System.IntegrationTests.TestSupport;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection("Integration tests")]
public sealed class TaxControllerTests : IntegrationTestBase
{
    public TaxControllerTests(ApiTestFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task TaxCrudScenario_exercises_happy_path_routes()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        (await client.GetAsync("/api/tax?pageNum=0&pageSize=10")).StatusCode.Should().Be(HttpStatusCode.OK);

        var createRequest = new TaxRequest
        {
            Name = "Integration Tax",
            Rate = 7,
            IsPercentage = true
        };

        var createResponse = await client.PostAsJsonAsync("/api/tax", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Tax createdTax = await WithDbContextAsync(async context =>
            await context.Taxes.SingleAsync(tax => tax.Name == createRequest.Name));

        (await client.GetAsync($"/api/tax/{createdTax.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var updateRequest = new TaxRequest
        {
            Name = "Updated Integration Tax",
            Rate = 8,
            IsPercentage = true
        };

        var updateResponse = await client.PutAsJsonAsync($"/api/tax/{createdTax.Id}", updateRequest);
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        Tax updatedTax = await WithDbContextAsync(async context =>
            await context.Taxes.SingleAsync(tax => tax.Name == updateRequest.Name));

        (await client.PutAsJsonAsync($"/api/tax/{updatedTax.Id}/link?itemsAreProducts=true", new[] { 4 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/tax/item/{updatedTax.TaxId}?isProduct=true")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsJsonAsync($"/api/tax/{updatedTax.Id}/unlink?itemsAreProducts=true", new[] { 4 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.DeleteAsync($"/api/tax/{updatedTax.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        await WithDbContextAsync(async context =>
        {
            var deletedTax = await context.Taxes.SingleAsync(tax => tax.Id == updatedTax.Id);
            deletedTax.IsDeleted.Should().BeTrue();
        });
    }

    [Fact]
    public async Task TaxEndpoints_return_validation_and_not_found_errors()
    {
        await ResetDatabaseAsync();

        var client = CreateClient(TestClaims.All);

        var invalidRequest = new TaxRequest
        {
            Name = string.Empty,
            Rate = 500,
            IsPercentage = true
        };

        (await client.PostAsJsonAsync("/api/tax", invalidRequest)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/tax/99999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
