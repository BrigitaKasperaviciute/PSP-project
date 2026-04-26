namespace POS_System.Integration.Tests.Tests;

public class AuthControllerTests(CustomWebApplicationFactory factory)
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_ValidRequest_ReturnsOk()
    {
        var request = new
        {
            Email = "register_test@example.com",
            UserName = "register_testuser",
            FirstName = "Test",
            LastName = "User",
            Password = "Test1234!",
            PhoneNumber = "123456789",
            BirthDate = "1990-01-01",
            RoleId = 0
        };

        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsBadRequest()
    {
        var request = new
        {
            Email = "dup_user@example.com",
            UserName = "dup_user",
            FirstName = "Dup",
            LastName = "User",
            Password = "Test1234!",
            PhoneNumber = "111222333",
            BirthDate = "1990-01-01",
            RoleId = 0
        };

        await _client.PostAsJsonAsync("/api/employees/register", request);
        var response = await _client.PostAsJsonAsync("/api/employees/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkWithToken()
    {
        var registerRequest = new
        {
            Email = "login_test@example.com",
            UserName = "login_testuser",
            FirstName = "Login",
            LastName = "Test",
            Password = "Test1234!",
            PhoneNumber = "987654321",
            BirthDate = "1990-01-01",
            RoleId = 0
        };
        await _client.PostAsJsonAsync("/api/employees/register", registerRequest);

        var loginRequest = new { UserName = "login_testuser", Password = "Test1234!" };
        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("jwtToken");
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorized()
    {
        var loginRequest = new { UserName = "nonexistent_user_xyz", Password = "WrongPassword1!" };

        var response = await _client.PostAsJsonAsync("/v1/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ForgotPassword_AnyEmail_ReturnsOk()
    {
        var request = new { Email = "anyone@example.com" };

        var response = await _client.PostAsJsonAsync("/forgot-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetPassword_NonExistentUser_ReturnsBadRequest()
    {
        var request = new { Email = "doesnotexist@example.com", ResetCode = "invalid-token", NewPassword = "Test1234!" };

        var response = await _client.PostAsJsonAsync("/reset-password", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
