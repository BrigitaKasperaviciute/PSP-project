using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace POS_System.Integration.Tests.Infrastructure;

public static class TestAuthHelper
{
    private const string SecretKey = "X7#pWq@8!Lm$Yr92&K^Jo4F%AbZ*tNcQ";
    private const string Issuer = "https://auth.POS-System.com";
    private const string Audience = "https://business.com";

    // Returns a signed JWT with the requested claim names (each with value "true").
    public static string GenerateToken(params string[] claimNames)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = claimNames
            .Select(name => new Claim(name, "true"))
            .ToList();

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // Convenience: token carrying all permissions used across tests.
    public static string GenerateFullAccessToken() => GenerateToken(
        "TransactionWrite", "TransactionRead",
        "HistoricTransactionWrite", "HistoricTransactionRead",
        "ServiceWrite", "ServiceRead",
        "ItemWrite", "ItemRead",
        "EmployeesWrite", "EmployeesRead",
        "TaxWrite", "TaxRead",
        "GiftCardWrite", "GiftCardRead",
        "CartItemWrite", "CartItemRead",
        "ItemDiscountRead", "ItemDiscountWrite",
        "BusinessDetailsRead", "BusinessDetailsWrite");

    // Convenience: build a pre-configured HttpClient with the full-access token attached.
    public static void AddFullAccessAuth(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", GenerateFullAccessToken());
    }
}
