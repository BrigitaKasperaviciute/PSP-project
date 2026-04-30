using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace POS_System.IntegrationTests.Helpers;

public static class JwtTokenHelper
{
    public const string SecretKey = "TestSecretKeyForPosSystemIntegrationTests!@#$%^&*()";
    public const string Issuer = "TestIssuer";
    public const string Audience = "TestAudience";

    public static readonly string[] AllClaims =
    [
        "TransactionWrite", "TransactionRead",
        "HistoricTransactionWrite", "HistoricTransactionRead",
        "ServiceWrite", "ServiceRead",
        "ItemWrite", "ItemRead",
        "EmployeesWrite", "EmployeesRead",
        "TaxWrite", "TaxRead",
        "HistoricWrite", "HistoricRead",
        "GiftCardWrite", "GiftCardRead",
        "CartItemWrite", "CartItemRead",
        "ItemDiscountRead", "ItemDiscountWrite",
        "BusinessDetailsRead", "BusinessDetailsWrite"
    ];

    public static string GenerateToken(params string[] claimNames)
    {
        var claims = claimNames.Select(name => new Claim(name, "Y")).ToList();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateFullAccessToken() => GenerateToken(AllClaims);
}
