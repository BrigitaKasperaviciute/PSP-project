using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace POS_System.IntegrationTests.Helpers;

public static class JwtTokenHelper
{
    private const string SecretKey = "X7#pWq@8!Lm$Yr92&K^Jo4F%AbZ*tNcQ";
    private const string Issuer = "https://auth.POS-System.com";
    private const string Audience = "https://business.com";

    public static readonly string FullAccessToken = GenerateToken(
        "TaxRead", "TaxWrite",
        "ItemRead", "ItemWrite",
        "ServiceRead", "ServiceWrite",
        "CartItemRead", "CartItemWrite",
        "GiftCardRead", "GiftCardWrite",
        "ItemDiscountRead", "ItemDiscountWrite",
        "EmployeesRead", "EmployeesWrite",
        "BusinessDetailsRead", "BusinessDetailsWrite",
        "TransactionRead", "TransactionWrite"
    );

    public static string GenerateToken(params string[] claimTypes)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = claimTypes.Select(c => new Claim(c, "Y")).ToList();

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
