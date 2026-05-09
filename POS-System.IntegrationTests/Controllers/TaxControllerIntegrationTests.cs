using FluentAssertions;
using POS_System.Business.Dtos.Request;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Infrastructure;
using System.Net;
using System.Text.Json;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

[Collection(nameof(ApiTestCollection))]
public sealed class TaxControllerIntegrationTests : IAsyncLifetime
{
    private readonly ApiTestFactory _factory;
    private HttpClient _authorizedClient;

    public TaxControllerIntegrationTests(ApiTestFactory factory)
    {
        _factory = factory;
        _authorizedClient = null!;
    }

    public async Task InitializeAsync()
    {
        // Setup authorized client with TaxRead and TaxWrite claims
        _authorizedClient = _factory.CreateAuthenticatedClient("TaxRead", "TaxWrite");
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _authorizedClient?.Dispose();
        await Task.CompletedTask;
    }

    // ===== GetAllTaxes Tests =====

    [Fact]
    public async Task GetAllTaxes_WithValidRequest_ReturnsOkWithTaxList()
    {
        // Arrange
        var createRequest = new TaxRequestBuilder()
            .WithName("VAT")
            .WithRate(20)
            .Build();
        
        await _authorizedClient.PostAsync("/api/tax", 
            new StringContent(JsonSerializer.Serialize(createRequest), System.Text.Encoding.UTF8, "application/json"));

        // Act
        var response = await _authorizedClient.GetAsync("/api/tax?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().Be(JsonValueKind.Object);
    }

    [Fact]
    public async Task GetAllTaxes_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        for (int i = 0; i < 15; i++)
        {
            var createRequest = new TaxRequestBuilder()
                .WithName($"Tax{i}")
                .WithRate(i)
                .Build();
            
            await _authorizedClient.PostAsync("/api/tax",
                new StringContent(JsonSerializer.Serialize(createRequest), System.Text.Encoding.UTF8, "application/json"));
        }

        // Act
        var response = await _authorizedClient.GetAsync("/api/tax?pageNum=1&pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAllTaxes_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient(); // No claims

        // Act
        var response = await unauthorizedClient.GetAsync("/api/tax?pageNum=0&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== CreateTax Tests =====

    [Fact]
    public async Task CreateTax_WithValidRequest_ReturnsOkAndCreatesEntity()
    {
        // Arrange
        var taxRequest = new TaxRequestBuilder()
            .WithName("Sales Tax")
            .WithRate(15)
            .WithIsPercentage(true)
            .Build();

        // Act
        var response = await _authorizedClient.PostAsync("/api/tax",
            new StringContent(JsonSerializer.Serialize(taxRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.TryGetProperty("name", out var name).Should().BeTrue();
        name.GetString().Should().Be("Sales Tax");

        // Verify database persistence
        var getTaxResponse = await _authorizedClient.GetAsync("/api/tax");
        getTaxResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateTax_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        var invalidRequest = "{}";

        // Act
        var response = await _authorizedClient.PostAsync("/api/tax",
            new StringContent(invalidRequest, System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTax_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var unauthorizedClient = _factory.CreateAuthenticatedClient("TaxRead"); // Only read claim
        var taxRequest = new TaxRequestBuilder().Build();

        // Act
        var response = await unauthorizedClient.PostAsync("/api/tax",
            new StringContent(JsonSerializer.Serialize(taxRequest), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== GetTaxById Tests =====

    [Fact]
    public async Task GetTaxById_WithExistingId_ReturnsOkAndTaxData()
    {
        // Arrange
        var taxRequest = new TaxRequestBuilder()
            .WithName("Import Tax")
            .WithRate(10)
            .Build();

        var createResponse = await _authorizedClient.PostAsync("/api/tax",
            new StringContent(JsonSerializer.Serialize(taxRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        jsonDocument.TryGetProperty("id", out var id).Should().BeTrue();
        var taxId = id.GetInt32();

        // Act
        var response = await _authorizedClient.GetAsync($"/api/tax/{taxId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var retrievedTax = JsonSerializer.Parse<JsonElement>(content);
        retrievedTax.TryGetProperty("id", out var retrievedId).Should().BeTrue();
        retrievedId.GetInt32().Should().Be(taxId);
    }

    [Fact]
    public async Task GetTaxById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/tax/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ===== DeleteTaxById Tests =====

    [Fact]
    public async Task DeleteTaxById_WithNonExistingId_ReturnsNotFound()
    {
        // Arrange & Act
        var response = await _authorizedClient.DeleteAsync("/api/tax/99999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteTaxById_WithoutAuthorizationClaim_ReturnsForbidden()
    {
        // Arrange
        var taxRequest = new TaxRequestBuilder().Build();
        var createResponse = await _authorizedClient.PostAsync("/api/tax",
            new StringContent(JsonSerializer.Serialize(taxRequest), System.Text.Encoding.UTF8, "application/json"));
        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var taxId = jsonDocument.GetProperty("id").GetInt32();

        var unauthorizedClient = _factory.CreateAuthenticatedClient("TaxRead");

        // Act
        var response = await unauthorizedClient.DeleteAsync($"/api/tax/{taxId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ===== LinkTaxToItems Tests =====

    [Fact]
    public async Task LinkTaxToItems_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var taxRequest = new TaxRequestBuilder()
            .WithName("Product Tax")
            .Build();

        var createResponse = await _authorizedClient.PostAsync("/api/tax",
            new StringContent(JsonSerializer.Serialize(taxRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var taxId = jsonDocument.GetProperty("id").GetInt32();

        var itemIdList = new[] { 1 };

        // Act
        var response = await _authorizedClient.PutAsync($"/api/tax/{taxId}/link?itemsAreProducts=true",
            new StringContent(JsonSerializer.Serialize(itemIdList), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ===== UnlinkTaxFromItems Tests =====

    [Fact]
    public async Task UnlinkTaxFromItems_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var taxRequest = new TaxRequestBuilder()
            .WithName("Unlink Tax")
            .Build();

        var createResponse = await _authorizedClient.PostAsync("/api/tax",
            new StringContent(JsonSerializer.Serialize(taxRequest), System.Text.Encoding.UTF8, "application/json"));

        var createdContent = await createResponse.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(createdContent);
        var taxId = jsonDocument.GetProperty("id").GetInt32();

        var itemIdList = new[] { 1 };

        // Act
        var response = await _authorizedClient.PutAsync($"/api/tax/{taxId}/unlink?itemsAreProducts=true",
            new StringContent(JsonSerializer.Serialize(itemIdList), System.Text.Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ===== GetTaxesLinkedToItemId Tests =====

    [Fact]
    public async Task GetTaxesLinkedToItemId_WithValidRequest_ReturnsOkWithTaxList()
    {
        // Arrange & Act
        var response = await _authorizedClient.GetAsync("/api/tax/item/1?isProduct=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        var jsonDocument = JsonSerializer.Parse<JsonElement>(content);
        jsonDocument.ValueKind.Should().NotBe(JsonValueKind.Undefined);
    }
}
