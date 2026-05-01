using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.Data.Database;
using POS_System.IntegrationTests;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class CartControllerTests : IClassFixture<PosSystemApiFactory>, IAsyncLifetime
{
    private readonly PosSystemApiFactory _factory;
    // CartController has no authorization requirements
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CartControllerTests(PosSystemApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAnonymousClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── GET /api/carts ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_WhenCartsExist_ReturnsOkWithPagedResult()
    {
        // Arrange — seed data contains 4 carts

        // Act
        var response = await _client.GetAsync("/api/carts");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<CartResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged.Should().NotBeNull();
        paged!.Results.Should().NotBeEmpty();
        paged.TotalCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetAll_WithPagination_ReturnsCorrectPageSize()
    {
        // Arrange
        const int pageSize = 2;

        // Act
        var response = await _client.GetAsync($"/api/carts?pageSize={pageSize}&pageNum=0");
        var body = await response.Content.ReadAsStringAsync();
        var paged = JsonSerializer.Deserialize<PagedResponse<CartResponse>>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        paged!.Results.Count().Should().BeLessOrEqualTo(pageSize);
        paged.PageSize.Should().Be(pageSize);
    }

    // ── GET /api/carts/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetById_WithExistingId_ReturnsOkWithCart()
    {
        // Arrange — cart with Id=1 is seeded (PENDING status)

        // Act
        var response = await _client.GetAsync("/api/carts/1");
        var body = await response.Content.ReadAsStringAsync();
        var cart = JsonSerializer.Deserialize<CartResponse>(body, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        cart.Should().NotBeNull();
        cart!.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetById_WithNonExistentId_ReturnsServerError()
    {
        // Arrange — cart 9999 does not exist; CartService throws a plain Exception
        // which GlobalExceptionHandler maps to 500

        // Act
        var response = await _client.GetAsync("/api/carts/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── POST /api/carts ──────────────────────────────────────────────────────

    [Fact]
    public async Task Create_WithValidData_ReturnsOkAndPersistsCart()
    {
        // Arrange — EmployeeVersionId=1 references a seeded employee row
        var request = new CartRequest { EmployeeVersionId = 1 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/carts", request);
        var body = await response.Content.ReadAsStringAsync();
        var cart = JsonSerializer.Deserialize<CartResponse>(body, JsonOptions);

        // Assert – HTTP layer
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        cart.Should().NotBeNull();
        cart!.EmployeeVersionId.Should().Be(1);
        cart.Id.Should().BeGreaterThan(0);

        // Assert – database state
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Carts.FindAsync(cart.Id);
        persisted.Should().NotBeNull();
        persisted!.EmployeeVersionId.Should().Be(1);
    }

    // ── DELETE /api/carts/{id} ───────────────────────────────────────────────

    [Fact]
    public async Task Delete_WithInProgressCart_ReturnsOk()
    {
        // Arrange — cart Id=3 is IN_PROGRESS (deletable)

        // Act
        var response = await _client.DeleteAsync("/api/carts/3");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify database state — cart should be gone
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cart = await db.Carts.FindAsync(3);
        cart.Should().BeNull();
    }

    [Fact]
    public async Task Delete_WithNonExistentId_ReturnsServerError()
    {
        // Arrange — cart 9999 does not exist

        // Act
        var response = await _client.DeleteAsync("/api/carts/9999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    // ── PATCH /api/carts/{id}/discount ───────────────────────────────────────

    [Fact]
    public async Task ApplyDiscount_WithNonExistentCart_ReturnsServerError()
    {
        // Arrange
        var request = new ApplyDiscountRequest("NONEXISTENT");

        // Act
        var response = await _client.PatchAsJsonAsync("/api/carts/9999/discount", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }
}
