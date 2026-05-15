using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS_System.Common;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Infrastructure;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiCollection))]
[Trait("Category", "Integration")]
public sealed class CartControllerTests : IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CartControllerTests(ApiFactory factory)
    {
        _factory = factory;
        // Cart endpoints have no [Authorize]
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await using var db = _factory.CreateDbContext();
        await db.ServiceReservations.ExecuteDeleteAsync();
        await db.ProductModificationOnCartItems.ExecuteDeleteAsync();
        await db.CartItems.ExecuteDeleteAsync();
        await db.Carts.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- GetAll ---

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        // Arrange
        var postResponse = await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build());
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"POST /api/carts failed: {await postResponse.Content.ReadAsStringAsync()}");

        // Act
        var response = await _client.GetAsync("/api/carts?pageNum=0&pageSize=10");

        // Assert
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"Response: {body}");
    }

    // --- GetById ---

    [Fact]
    public async Task GetById_WithExistingId_ReturnsCart()
    {
        // Arrange
        var postResponse = await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build());
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"POST failed: {await postResponse.Content.ReadAsStringAsync()}");
        var created = await postResponse.Content.ReadFromJsonAsync<CartDto>();

        // Act
        var response = await _client.GetAsync($"/api/carts/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CartDto>();
        body!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/carts/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- Create ---

    [Fact]
    public async Task Create_WithValidEmployeeId_ReturnsOkAndPersistsCart()
    {
        // Arrange
        var request = new CartBuilder().WithEmployeeVersionId(1).Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);

        // Assert - HTTP
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Response: {await response.Content.ReadAsStringAsync()}");
        var body = await response.Content.ReadFromJsonAsync<CartDto>();
        body!.EmployeeVersionId.Should().Be(1);

        // Assert - database
        await using var db = _factory.CreateDbContext();
        var persisted = await db.Carts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == body.Id);
        persisted.Should().NotBeNull();
    }

    // --- Delete ---

    [Fact]
    public async Task Delete_WithExistingId_ReturnsOk()
    {
        // Arrange
        var postResponse = await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build());
        postResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            $"POST failed: {await postResponse.Content.ReadAsStringAsync()}");
        var created = await postResponse.Content.ReadFromJsonAsync<CartDto>();

        // Act
        var response = await _client.DeleteAsync($"/api/carts/{created!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _factory.CreateDbContext();
        var inDb = await db.Carts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == created.Id);
        inDb.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_Returns404()
    {
        // Act
        var response = await _client.DeleteAsync("/api/carts/999999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var error = await response.Content.ReadFromJsonAsync<ErrorDetails>();
        error!.Status.Should().Be((int)HttpStatusCode.NotFound);
    }

    // --- GetDiscount ---

    [Fact]
    public async Task GetDiscount_WithCartHavingNoDiscount_ReturnsOkWithNullBody()
    {
        // Arrange
        var postResponse = await _client.PostAsJsonAsync("/api/carts", new CartBuilder().Build());
        var created = await postResponse.Content.ReadFromJsonAsync<CartDto>();

        // Act
        var response = await _client.GetAsync($"/api/carts/{created!.Id}/discount");

        // Assert – controller always returns Ok(null) when no discount is assigned
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

// CartResponse has `required` properties that cause STJ deserialization to throw
// when the server omits null values. Use this minimal DTO for test assertions instead.
file record CartDto(int Id, int EmployeeVersionId);
