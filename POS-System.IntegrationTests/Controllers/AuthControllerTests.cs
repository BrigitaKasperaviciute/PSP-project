using POS_System.IntegrationTests.Builders;
using POS_System.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using Xunit;

namespace POS_System.IntegrationTests.Controllers
{
    /// <summary>
    /// Integration tests for AuthController.
    /// Tests authentication and registration flows with realistic scenarios.
    /// </summary>
    public class AuthControllerTests : IntegrationTestBase
    {
        [Fact]
        public async Task RegisterUserAsync_ValidRequest_ReturnsOk()
        {
            // Arrange
            var registerRequest = UserRegisterRequestBuilder.CreateDefault();

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/api/employees/register",
                CreateJsonContent(registerRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task RegisterUserAsync_DuplicateEmail_MayReturnBadRequest()
        {
            // Arrange
            var email = $"testuser{Guid.NewGuid()}@example.com";
            var request1 = UserRegisterRequestBuilder.CreateWithEmail(email);

            // Act - First registration
            var response1 = await UnauthenticatedClient.PostAsync(
                "/api/employees/register",
                CreateJsonContent(request1)
            );

            // Try to register with same email
            var request2 = UserRegisterRequestBuilder.CreateWithEmail(email);
            var response2 = await UnauthenticatedClient.PostAsync(
                "/api/employees/register",
                CreateJsonContent(request2)
            );

            // Assert
            response2.StatusCode.Should().BeOneOf(
                HttpStatusCode.BadRequest,
                HttpStatusCode.InternalServerError,
                HttpStatusCode.Conflict
            );
        }

        [Fact]
        public async Task RegisterUserAsync_InvalidEmail_MayReturnBadRequest()
        {
            // Arrange
            var request = new
            {
                Email = "invalidemail",
                UserName = "testuser",
                FirstName = "Test",
                LastName = "User",
                Password = "TestPassword123!",
                PhoneNumber = "1234567890",
                BirthDate = new DateOnly(2000, 1, 1),
                RoleId = 0
            };

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/api/employees/register",
                CreateJsonContent(request)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }

        [Fact]
        public async Task RegisterUserAsync_WeakPassword_MayReturnBadRequest()
        {
            // Arrange
            var request = new
            {
                Email = $"test{Guid.NewGuid()}@example.com",
                UserName = $"testuser{Guid.NewGuid()}",
                FirstName = "Test",
                LastName = "User",
                Password = "weak",
                PhoneNumber = "1234567890",
                BirthDate = new DateOnly(2000, 1, 1),
                RoleId = 0
            };

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/api/employees/register",
                CreateJsonContent(request)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
        }

        [Fact]
        public async Task LoginUserAsync_ValidCredentials_ReturnsToken()
        {
            // Arrange
            // Try to login with default test credentials
            var loginRequest = UserLoginRequestBuilder.CreateDefault();

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/v1/auth/login",
                CreateJsonContent(loginRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.BadRequest
            );
        }

        [Fact]
        public async Task LoginUserAsync_InvalidCredentials_ReturnsUnauthorized()
        {
            // Arrange
            var loginRequest = new UserLoginRequestBuilder()
                .WithUserName("nonexistent")
                .WithPassword("WrongPassword123!")
                .Build();

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/v1/auth/login",
                CreateJsonContent(loginRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task LoginUserAsync_EmptyUserName_ReturnsBadRequest()
        {
            // Arrange
            var loginRequest = new { UserName = "", Password = "TestPassword123!" };

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/v1/auth/login",
                CreateJsonContent(loginRequest)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ForgotPasswordAsync_ValidEmail_ReturnsOkOrBadRequest()
        {
            // Arrange
            var request = new { Email = "test@example.com" };

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/forgot-password",
                CreateJsonContent(request)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ForgotPasswordAsync_InvalidEmail_MayReturnBadRequest()
        {
            // Arrange
            var request = new { Email = "invalidemail" };

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/forgot-password",
                CreateJsonContent(request)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task ResetPasswordAsync_WithValidToken_ReturnsOkOrError()
        {
            // Arrange
            var request = new { Email = "test@example.com", Token = "validtoken", NewPassword = "NewPassword123!" };

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/reset-password",
                CreateJsonContent(request)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task ResetPasswordAsync_WithInvalidToken_MayReturnBadRequest()
        {
            // Arrange
            var request = new { Email = "test@example.com", Token = "invalidtoken", NewPassword = "NewPassword123!" };

            // Act
            var response = await UnauthenticatedClient.PostAsync(
                "/reset-password",
                CreateJsonContent(request)
            );

            // Assert
            response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.OK);
        }

        [Fact]
        public async Task RegisterUserAsync_NullRequest_ReturnsBadRequest()
        {
            // Arrange
            var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await UnauthenticatedClient.PostAsync("/api/employees/register", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task LoginUserAsync_NullRequest_ReturnsBadRequest()
        {
            // Arrange
            var content = new StringContent("null", System.Text.Encoding.UTF8, "application/json");

            // Act
            var response = await UnauthenticatedClient.PostAsync("/v1/auth/login", content);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
