using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using POS_System.IntegrationTests.Infrastructure;
using POS_System.Data.Identity;

namespace POS_System.IntegrationTests.Controllers;

public class AuthControllerIntegrationTests
{
    [Fact]
    public async Task LoginUser_ValidCredentials_ReturnsOkAndKeepsUserState()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var userName = IntegrationTestHelpers.Unique("integration-user");
        var email = $"{userName}@example.com";
        const string password = "P@ssw0rd!";

        // Arrange
        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            const string roleName = "Cashier";

            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = roleName, NormalizedName = roleName.ToUpperInvariant() });
                roleResult.Succeeded.Should().BeTrue();

                var role = await roleManager.FindByNameAsync(roleName);
                role.Should().NotBeNull();

                var claimResult = await roleManager.AddClaimAsync(role!, new Claim("TransactionRead", "Y"));
                claimResult.Succeeded.Should().BeTrue();
            }

            var user = new ApplicationUser
            {
                FirstName = "Integration",
                LastName = "User",
                BirthDate = new DateOnly(1993, 5, 1),
                UserName = userName,
                Email = email,
                PhoneNumber = "+421900000123",
                RoleId = 1,
                EmployeeId = 9999,
                StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                Version = DateTime.UtcNow,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var createResult = await userManager.CreateAsync(user, password);
            createResult.Succeeded.Should().BeTrue();

            var addRoleResult = await userManager.AddToRoleAsync(user, roleName);
            addRoleResult.Succeeded.Should().BeTrue();
        }

        var usersBeforeLogin = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());
        var payload = new { userName, password };

        // Act
        var response = await client.PostAsJsonAsync("/v1/auth/login", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await IntegrationTestHelpers.ReadJsonAsync(response);
        var hasJwtToken = json.RootElement.TryGetProperty("jwtToken", out var jwtTokenElement);
        hasJwtToken.Should().BeTrue();
        jwtTokenElement.GetString().Should().NotBeNullOrWhiteSpace();

        var exists = await factory.ExecuteDbContextAsync(db =>
            db.Users.AnyAsync(x => x.UserName == userName));
        var usersAfterLogin = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());

        exists.Should().BeTrue();
        usersAfterLogin.Should().Be(usersBeforeLogin);
    }

    [Fact]
    public async Task RegisterUser_InvalidJson_ReturnsBadRequestAndKeepsDatabaseUnchanged()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Arrange
        var countBefore = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());

        // Act
        var response = await IntegrationTestHelpers.PostMalformedJsonAsync(client, "/api/employees/register");

        // Assert
        await IntegrationTestHelpers.AssertBadRequestAsync(response);

        var countAfter = await factory.ExecuteDbContextAsync(db => db.Users.CountAsync());
        countAfter.Should().Be(countBefore);
    }
}
