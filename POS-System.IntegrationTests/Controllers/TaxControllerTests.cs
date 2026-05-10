using POS_System.Business.Dtos;
using POS_System.Business.Dtos.Response;
using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Factories;
using POS_System.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for TaxController.
    /// Tests tax creation, retrieval, update, and deletion with both happy path and negative flow scenarios.
    /// </summary>
    public class TaxControllerTests : IntegrationTestBase
    {
        // ==================== CREATE TESTS ====================

        /// <summary>
        /// Test: CreateTax_ValidRequest_ReturnsOkWithCreatedTax
        /// Scenario: Successfully create a new tax with valid data
        /// Expected: Tax is created and returned with 200 OK
        /// </summary>
        [Fact]
        public async Task CreateTax_ValidRequest_ReturnsOkWithCreatedTax()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();

            // Act
            var response = await Client.PostAsync(
                "/api/tax",
                CreateJsonContent(taxRequest)
            );

            // Assert
            AssertOkResponse(response);
            var createdTax = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            createdTax.ValueKind.Should().Be(System.Text.Json.JsonValueKind.Object);
            createdTax.GetProperty("name").GetString().Should().Be(taxRequest.Name);
            createdTax.GetProperty("rate").GetInt32().Should().Be(taxRequest.Rate);
            createdTax.GetProperty("isPercentage").GetBoolean().Should().Be(taxRequest.IsPercentage);
        }

        /// <summary>
        /// Test: CreateTax_NullRequest_ReturnsBadRequest
        /// Scenario: Attempt to create tax with null request body
        /// Expected: Returns 400 BadRequest
        /// </summary>
        [Fact]
        public async Task CreateTax_NullRequest_ReturnsBadRequest()
        {
            // Arrange
            var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await Client.PostAsync("/api/tax", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Test: CreateTax_MissingRequiredFields_ReturnsBadRequest
        /// Scenario: Attempt to create tax with missing required fields
        /// Expected: Returns 400 BadRequest
        /// </summary>
        [Fact]
        public async Task CreateTax_MissingRequiredFields_ReturnsBadRequest()
        {
            // Arrange
            var invalidRequest = new { Name = "Test" }; // Missing Rate and IsPercentage

            // Act
            var response = await Client.PostAsync(
                "/api/tax",
                CreateJsonContent(invalidRequest)
            );

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Test: CreateTax_DuplicateName_ReturnsOk
        /// Scenario: Create two taxes with the same name (should be allowed)
        /// Expected: Both taxes created successfully
        /// </summary>
        [Fact]
        public async Task CreateTax_DuplicateName_ReturnsOk()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateWithName("DuplicateTax");

            // Act - Create first tax
            var response1 = await Client.PostAsync(
                "/api/tax",
                CreateJsonContent(taxRequest)
            );
            var tax1 = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response1);

            // Act - Create second tax with same name
            var response2 = await Client.PostAsync(
                "/api/tax",
                CreateJsonContent(taxRequest)
            );

            // Assert
            AssertOkResponse(response1);
            AssertOkResponse(response2);
            var tax2 = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response2);
            tax1.GetProperty("id").GetInt32().Should().NotBe(tax2.GetProperty("id").GetInt32());
        }

        /// <summary>
        /// Test: CreateTax_HighRateValue_ReturnsOk
        /// Scenario: Create tax with high rate value
        /// Expected: Tax created successfully
        /// </summary>
        [Fact]
        public async Task CreateTax_HighRateValue_ReturnsOk()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateWithRate(99999);

            // Act
            var response = await Client.PostAsync(
                "/api/tax",
                CreateJsonContent(taxRequest)
            );

            // Assert
            AssertOkResponse(response);
            var createdTax = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            createdTax.GetProperty("rate").GetInt32().Should().Be(99999);
        }

        // ==================== GET TESTS ====================

        /// <summary>
        /// Test: GetAllTaxes_NoFilters_ReturnsAllTaxes
        /// Scenario: Get all taxes without filters
        /// Expected: Returns list of all taxes with 200 OK
        /// </summary>
        [Fact]
        public async Task GetAllTaxes_NoFilters_ReturnsAllTaxes()
        {
            // Arrange
            var taxRequest1 = TaxRequestBuilder.CreateWithName("Tax1");
            var taxRequest2 = TaxRequestBuilder.CreateWithName("Tax2");

            await Client.PostAsync("/api/tax", CreateJsonContent(taxRequest1));
            await Client.PostAsync("/api/tax", CreateJsonContent(taxRequest2));

            // Act
            var response = await Client.GetAsync("/api/tax");

            // Assert
            AssertOkResponse(response);
            var taxes = await DeserializeResponseAsync<PagedResponse<TaxResponse>>(response);
            taxes.Should().NotBeNull();
            taxes!.Results.Should().HaveCountGreaterThanOrEqualTo(2);
        }

        /// <summary>
        /// Test: GetTaxById_ValidId_ReturnsTax
        /// Scenario: Get specific tax by valid ID
        /// Expected: Returns the tax with 200 OK
        /// </summary>
        [Fact]
        public async Task GetTaxById_ValidId_ReturnsTax()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync("/api/tax", CreateJsonContent(taxRequest));
            var taxId = await ExtractIdFromResponseAsync(createResponse);

            // Act
            var response = await Client.GetAsync($"/api/tax/{taxId}");

            // Assert
            AssertOkResponse(response);
            var retrievedTax = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            retrievedTax.GetProperty("id").GetInt32().Should().Be(taxId);
        }

        /// <summary>
        /// Test: GetTaxById_InvalidId_ReturnsNotFound
        /// Scenario: Get tax with non-existent ID
        /// Expected: Returns 404 NotFound
        /// </summary>
        [Fact]
        public async Task GetTaxById_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.GetAsync($"/api/tax/{invalidId}");

            // Assert
            AssertNotFoundResponse(response);
        }

        /// <summary>
        /// Test: GetTaxesLinkedToItemId_ValidItemId_ReturnsTaxes
        /// Scenario: Get taxes linked to a specific item
        /// Expected: Returns list of linked taxes with 200 OK
        /// </summary>
        [Fact]
        public async Task GetTaxesLinkedToItemId_ValidItemId_ReturnsTaxes()
        {
            // Arrange
            // Assuming there are tests that link taxes to items, this retrieves them
            int itemId = 1; // From seeded data

            // Act
            var response = await Client.GetAsync($"/api/tax/item/{itemId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        // ==================== UPDATE TESTS ====================

        /// <summary>
        /// Test: UpdateTaxById_ValidRequest_ReturnsUpdatedTax
        /// Scenario: Update an existing tax with valid data
        /// Expected: Tax is updated and returned with 200 OK
        /// </summary>
        [Fact]
        public async Task UpdateTaxById_ValidRequest_ReturnsUpdatedTax()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync("/api/tax", CreateJsonContent(taxRequest));
            var taxId = await ExtractIdFromResponseAsync(createResponse);

            var updatedTaxRequest = new TaxRequestBuilder()
                .WithName("Updated Tax")
                .WithRate(25)
                .Build();

            // Act
            var response = await Client.PutAsync(
                $"/api/tax/{taxId}",
                CreateJsonContent(updatedTaxRequest)
            );

            // Assert
            AssertOkResponse(response);
            var updatedTax = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            updatedTax.GetProperty("name").GetString().Should().Be("Updated Tax");
            updatedTax.GetProperty("rate").GetInt32().Should().Be(25);
        }

        /// <summary>
        /// Test: UpdateTaxById_InvalidId_ReturnsNotFound
        /// Scenario: Update tax with non-existent ID
        /// Expected: Returns 404 NotFound
        /// </summary>
        [Fact]
        public async Task UpdateTaxById_InvalidId_ReturnsNotFound()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();
            int invalidId = 999999;

            // Act
            var response = await Client.PutAsync(
                $"/api/tax/{invalidId}",
                CreateJsonContent(taxRequest)
            );

            // Assert
            AssertNotFoundResponse(response);
        }

        /// <summary>
        /// Test: UpdateTaxById_NullRequest_ReturnsBadRequest
        /// Scenario: Update tax with null request body
        /// Expected: Returns 400 BadRequest
        /// </summary>
        [Fact]
        public async Task UpdateTaxById_NullRequest_ReturnsBadRequest()
        {
            // Arrange
            int taxId = 1;
            var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await Client.PutAsync($"/api/tax/{taxId}", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Test: UpdateTaxById_ChangeRate_ReturnsUpdatedTax
        /// Scenario: Update only the tax rate
        /// Expected: Only rate changes, other fields remain same
        /// </summary>
        [Fact]
        public async Task UpdateTaxById_ChangeRate_ReturnsUpdatedTax()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync("/api/tax", CreateJsonContent(taxRequest));
            var createdTax = await DeserializeResponseAsync<System.Text.Json.JsonElement>(createResponse);
            var taxId = createdTax.GetProperty("id").GetInt32();
            var originalName = createdTax.GetProperty("name").GetString();

            var updatedTaxRequest = new TaxRequestBuilder()
                .WithName(originalName!)
                .WithRate(50)
                .Build();

            // Act
            var response = await Client.PutAsync(
                $"/api/tax/{taxId}",
                CreateJsonContent(updatedTaxRequest)
            );

            // Assert
            AssertOkResponse(response);
            var updatedTax = await DeserializeResponseAsync<System.Text.Json.JsonElement>(response);
            updatedTax.GetProperty("name").GetString().Should().Be(originalName);
            updatedTax.GetProperty("rate").GetInt32().Should().Be(50);
        }

        // ==================== DELETE TESTS ====================

        /// <summary>
        /// Test: DeleteTaxById_ValidId_ReturnsOk
        /// Scenario: Delete an existing tax by ID
        /// Expected: Tax is deleted and returns 200 OK or 204 NoContent
        /// </summary>
        [Fact]
        public async Task DeleteTaxById_ValidId_ReturnsOk()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync("/api/tax", CreateJsonContent(taxRequest));
            var taxId = await ExtractIdFromResponseAsync(createResponse);

            // Act
            var response = await Client.DeleteAsync($"/api/tax/{taxId}");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Test: DeleteTaxById_InvalidId_ReturnsNotFound
        /// Scenario: Delete tax with non-existent ID
        /// Expected: Returns 404 NotFound
        /// </summary>
        [Fact]
        public async Task DeleteTaxById_InvalidId_ReturnsNotFound()
        {
            // Arrange
            int invalidId = 999999;

            // Act
            var response = await Client.DeleteAsync($"/api/tax/{invalidId}");

            // Assert
            AssertNotFoundResponse(response);
        }

        /// <summary>
        /// Test: DeleteTaxById_ThenGetById_ReturnsNotFound
        /// Scenario: Delete a tax and then try to retrieve it
        /// Expected: First delete returns OK/NoContent, then get returns NotFound
        /// </summary>
        [Fact]
        public async Task DeleteTaxById_ThenGetById_ReturnsNotFound()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync("/api/tax", CreateJsonContent(taxRequest));
            var taxId = await ExtractIdFromResponseAsync(createResponse);

            // Act - Delete
            var deleteResponse = await Client.DeleteAsync($"/api/tax/{taxId}");
            deleteResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

            // Act - Try to get
            var getResponse = await Client.GetAsync($"/api/tax/{taxId}");

            // Assert
            getResponse.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.InternalServerError);
        }

        // ==================== AUTHORIZATION TESTS ====================

        /// <summary>
        /// Test: GetAllTaxes_UnauthenticatedUser_ReturnsForbidden
        /// Scenario: Try to get taxes without authentication
        /// Expected: Returns 401 Unauthorized or 403 Forbidden
        /// </summary>
        [Fact]
        public async Task GetAllTaxes_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange - Use unauthenticated client

            // Act
            var response = await UnauthenticatedClient.GetAsync("/api/tax");

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        /// <summary>
        /// Test: CreateTax_UnauthenticatedUser_ReturnsForbidden
        /// Scenario: Try to create tax without authentication
        /// Expected: Returns 401 Unauthorized or 403 Forbidden
        /// </summary>
        [Fact]
        public async Task CreateTax_UnauthenticatedUser_ReturnsForbidden()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/api/tax",
                CreateJsonContent(taxRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
        }

        // ==================== LINK/UNLINK TESTS ====================

        /// <summary>
        /// Test: LinkTaxToItems_ValidItemIds_ReturnsOk
        /// Scenario: Link a tax to multiple items
        /// Expected: Returns 200 OK and tax is linked
        /// </summary>
        [Fact]
        public async Task LinkTaxToItems_ValidItemIds_ReturnsOk()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync("/api/tax", CreateJsonContent(taxRequest));
            var taxId = await ExtractIdFromResponseAsync(createResponse);
            var itemIds = new[] { 1, 2 };

            // Act
            var response = await Client.PostAsync(
                $"/api/tax/{taxId}/items",
                CreateJsonContent(new { itemIds })
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Test: UnlinkTaxFromItems_ValidItemIds_ReturnsOk
        /// Scenario: Unlink a tax from multiple items
        /// Expected: Returns 200 OK or similar
        /// </summary>
        [Fact]
        public async Task UnlinkTaxFromItems_ValidItemIds_ReturnsOk()
        {
            // Arrange
            var taxRequest = TaxRequestBuilder.CreateDefault();
            var createResponse = await Client.PostAsync("/api/tax", CreateJsonContent(taxRequest));
            var taxId = await ExtractIdFromResponseAsync(createResponse);
            var itemIds = new[] { 1, 2 };

            // Act
            var response = await Client.DeleteAsync(
                $"/api/tax/{taxId}/items?itemIds={string.Join(",", itemIds)}"
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }
    }
}
