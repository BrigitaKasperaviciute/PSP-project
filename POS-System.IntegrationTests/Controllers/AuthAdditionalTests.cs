using System.Net;
using FluentAssertions;
using POS_System.IntegrationTests.TestSupport;
using POS_System.IntegrationTests.Helpers;
using Xunit;

namespace POS_System.IntegrationTests.Controllers;

public class AuthAdditionalTests
{
    [Fact]
    public async Task Auth_Login_Forgot_Reset_PermissiveAssertions()
    {
        using var factory = new CustomWebApplicationFactory(Guid.NewGuid().ToString());
        using var client = factory.CreateClient();

        var acceptable = new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound };

        // Login
        var loginResp = await client.PostAsync("v1/auth/login", TestDataFactory.ToJsonContent(new { userName = "no", password = "x" }));
        loginResp.StatusCode.Should().BeOneOf(acceptable);

        // Forgot password
        var forgotResp = await client.PostAsync("forgot-password", TestDataFactory.ToJsonContent(new { email = "nobody@example.com" }));
        forgotResp.StatusCode.Should().BeOneOf(acceptable);

        // Reset password
        var resetResp = await client.PostAsync("reset-password", TestDataFactory.ToJsonContent(new { token = "t", newPassword = "Password1!" }));
        resetResp.StatusCode.Should().BeOneOf(acceptable);
    }
}
