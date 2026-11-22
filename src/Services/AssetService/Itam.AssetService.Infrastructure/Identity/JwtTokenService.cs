

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Itam.AssetService.Application.Identity;
using Itam.Common.Configuration.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Itam.AssetService.Infrastructure.Identity
{
    public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
    {
        private readonly JwtOptions _jwt = options.Value;

        public (string Token, DateTime ExpiresUtc) Create(Guid userId, string email, IReadOnlyCollection<string> roles)
        {
            var now = DateTime.UtcNow;
            var expires = now.AddMinutes(_jwt.ExpirationMinutes);
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, email),
                new(ClaimTypes.Email, email),
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _jwt.Issuer,
                audience: _jwt.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: credentials);
    
            return (new JwtSecurityTokenHandler().WriteToken(token), expires);

        }
        
    }
}