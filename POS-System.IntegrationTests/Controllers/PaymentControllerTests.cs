using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class PaymentControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public PaymentControllerTests(ApiFactory factory)
    {
        _factory = factory;
        // Payment endpoints have no [Authorize]
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.Transactions.ExecuteDeleteAsync();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.Carts.ExecuteDeleteAsync();
        await db.ProductOnTaxes.ExecuteDeleteAsync();
        await db.ProductOnItemDiscounts.ExecuteDeleteAsync();
        await db.ProductModifications.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int cartId, int amount)> SetupCartWithProductAsync()
    {
        var productClient = _factory.CreateClientWithClaims("ItemRead", "ItemWrite");
        var product = await (await productClient.PostAsJsonAsync("/api/product",
                new ProductBuilder().WithPrice(1500).Build()))
            .Content.ReadFromJsonAsync<ProductResponse>();

        var cart = await (await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDto>();

        var cartItemClient = _factory.CreateClientWithClaims("CartItemRead", "CartItemWrite");
        await cartItemClient.PostAsJsonAsync($"/api/carts/{cart!.Id}/items",
            new CartItemRequest { CartId = cart.Id, Quantity = 1, IsProduct = true, ProductVersionId = product!.Id });

        return (cart.Id, product.Price);
    }

    // --- RegisterCash ---

    [Fact]
    public async Task RegisterCash_WithValidCartAndAmount_ReturnsOk()
    {
        // Arrange
        var (cartId, amount) = await SetupCartWithProductAsync();
        var request = new CashRequest(cartId, (ulong)amount, null, "TEST-REF-001", null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RegisterCash_WithNonExistentCart_Returns404()
    {
        var request = new CashRequest(999999, 10000ul, null, "TEST-REF-INVALID", null);

        var response = await _client.PostAsJsonAsync("/api/payments/cash", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // --- GetTransactionsByCart ---

    [Fact]
    public async Task GetTransactions_WithExistingCart_ReturnsOk()
    {
        // Arrange – create a cart (may or may not have transactions)
        var cart = await (await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build()))
            .Content.ReadFromJsonAsync<CartDto>();

        // Act
        var response = await _client.GetAsync($"/api/payments/{cart!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetTransactions_WithNonExistentCart_ReturnsOkWithEmptyList()
    {
        // Act – the repository returns an empty list for a cart with no transactions
        var response = await _client.GetAsync("/api/payments/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<IEnumerable<TransactionResponse>>();
        body.Should().NotBeNull();
        body!.Should().BeEmpty();
    }
}

file record CartDto(int Id, int EmployeeVersionId);
