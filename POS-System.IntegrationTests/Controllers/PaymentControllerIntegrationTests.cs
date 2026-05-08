using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.IntegrationTests.Infrastructure;

namespace POS_System.IntegrationTests.Controllers;

public class PaymentControllerIntegrationTests
{
    [Fact]
    public async Task RegisterCashTransaction_ValidPayload_PersistsTransactionAndReturnsOk()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var transactionRef = IntegrationTestHelpers.Unique("cash-ref");

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Transactions.CountAsync());
        var payload = new
        {
            cartId = 3,
            amount = 500UL,
            tip = 0,
            transactionRef,
            phoneNumber = "+421900000999"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/payments/cash", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("transaction");

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Transactions.CountAsync());
        countAfter.Should().Be(countBefore + 1);
    }

    [Fact]
    public async Task RegisterCashTransaction_InvalidJson_ReturnsBadRequestAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Transactions.CountAsync());

        // Act
        var response = await IntegrationTestHelpers.PostMalformedJsonAsync(client, "/api/payments/cash");

        // Assert
        await IntegrationTestHelpers.AssertBadRequestAsync(response);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Transactions.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
