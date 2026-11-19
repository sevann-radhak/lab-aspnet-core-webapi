using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace LabAspNetCoreWebApi.Api.Services;

public sealed class TokenService(IOptions<LabJwtOptions> options)
{
    public string CreateToken(string userName, string role)
    {
        LabJwtOptions jwt = options.Value;
        SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(jwt.SigningKey));
        SigningCredentials credentials = new(key, SecurityAlgorithms.HmacSha256);
        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, userName),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, role),
        ];

        JwtSecurityToken token = new(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
