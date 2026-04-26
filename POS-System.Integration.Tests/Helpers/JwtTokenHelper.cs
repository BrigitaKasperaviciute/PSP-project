using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace POS_System.Integration.Tests.Helpers;

public static class JwtTokenHelper
{
    public const string SecretKey = "X7#pWq@8!Lm$Yr92&K^Jo4F%AbZ*tNcQ";
    public const string Issuer = "https://auth.POS-System.com";
    public const string Audience = "https://business.com";

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
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
