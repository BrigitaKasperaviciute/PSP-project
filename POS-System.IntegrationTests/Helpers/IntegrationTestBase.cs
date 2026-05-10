using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using POS_System.Data.Database;
using POS_System.IntegrationTests.Factories;
using Xunit;

namespace POS_System.IntegrationTests.Helpers
{
    /// <summary>
    /// Base class for integration tests providing common functionality.
    /// </summary>
    public abstract class IntegrationTestBase : IAsyncLifetime
    {
        protected ApiTestFactory Factory = null!;
        protected HttpClient Client = null!;
        protected HttpClient UnauthenticatedClient = null!;

        /// <summary>
        /// Initializes the test factory and creates HTTP clients.
        /// </summary>
        public virtual async Task InitializeAsync()
        {
            Factory = new ApiTestFactory();
            Client = await Factory.CreateAuthenticatedClientAsync();
            UnauthenticatedClient = Factory.CreateUnauthenticatedClient();
        }

        /// <summary>
        /// Disposes the factory and clients after test completion.
        /// </summary>
        public virtual async Task DisposeAsync()
        {
            Client?.Dispose();
            UnauthenticatedClient?.Dispose();
            Factory?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Runs a database action inside a scoped ApplicationDbContext.
        /// </summary>
        protected async Task ExecuteDbAsync(Func<ApplicationDbContext, Task> action)
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await action(dbContext);
        }

        /// <summary>
        /// Runs a database query inside a scoped ApplicationDbContext and returns the result.
        /// </summary>
        protected async Task<TResult> ExecuteDbAsync<TResult>(Func<ApplicationDbContext, Task<TResult>> action)
        {
            using var scope = Factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await action(dbContext);
        }

        /// <summary>
        /// Deserializes JSON response content to the specified type.
        /// </summary>
        protected async Task<T?> DeserializeResponseAsync<T>(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content))
                return default;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<T>(content, options);
        }

        /// <summary>
        /// Deserializes JSON response content as a dynamic object.
        /// </summary>
        protected async Task<dynamic?> DeserializeResponseAsDynamicAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrEmpty(content))
                return null;

            return JsonSerializer.Deserialize<dynamic>(content);
        }

        /// <summary>
        /// Asserts that the response has an OK status code.
        /// </summary>
        protected void AssertOkResponse(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        /// <summary>
        /// Asserts that the response has a Created status code.
        /// </summary>
        protected void AssertCreatedResponse(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        /// <summary>
        /// Asserts that the response has a BadRequest status code.
        /// </summary>
        protected void AssertBadRequestResponse(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        /// <summary>
        /// Asserts that the response has an Unauthorized status code.
        /// </summary>
        protected void AssertUnauthorizedResponse(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Asserts that the response has a Forbidden status code.
        /// </summary>
        protected void AssertForbiddenResponse(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        /// <summary>
        /// Asserts that the response has a NotFound status code.
        /// </summary>
        protected void AssertNotFoundResponse(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Asserts that the response has a NoContent status code.
        /// </summary>
        protected void AssertNoContentResponse(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Asserts that the response has an InternalServerError status code.
        /// </summary>
        protected void AssertInternalServerErrorResponse(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }

        /// <summary>
        /// Parses integer ID from response JSON (looks for "id" field).
        /// </summary>
        protected async Task<int> ExtractIdFromResponseAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var doc = JsonDocument.Parse(content);
            
            if (doc.RootElement.TryGetProperty("id", out var idElement))
            {
                return idElement.GetInt32();
            }

            if (doc.RootElement.TryGetProperty("productId", out var productIdElement))
            {
                return productIdElement.GetInt32();
            }

            throw new Exception("Could not extract ID from response");
        }

        /// <summary>
        /// Creates a StringContent from an object with JSON serialization.
        /// </summary>
        protected StringContent CreateJsonContent(object obj)
        {
            var json = JsonSerializer.Serialize(obj);
            return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }
    }
}
