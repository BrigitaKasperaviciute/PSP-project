using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace POS_System.IntegrationTests.Infrastructure;

public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var parts = authHeader.ToString().Split(' ', 2);
        if (parts.Length != 2 || parts[0] != SchemeName)
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new Claim(ClaimTypes.Name, "testuser") };
        foreach (var name in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries))
            claims.Add(new Claim(name.Trim(), "Y"));

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)),
            SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
