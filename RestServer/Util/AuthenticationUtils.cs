using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RestServer.Infrastructure.AspNetCore;

namespace RestServer.Util
{
    internal static class AuthenticationUtils
    {
        public static (DateTime creationDate, DateTime expiryDate, string token) GenerateJwtTokenForUser(string userId, string email, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations)
        {
            ClaimsIdentity identity = new ClaimsIdentity
            (
                new GenericIdentity(userId, "Login"),
                // JWT Token was too heavy: We're only keeping ID and the random seed now.
                new[]
                {
                    new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                    new Claim(JwtRegisteredClaimNames.UniqueName, userId),
                    // new Claim(JwtRegisteredClaimNames.Email, email),
                }
            );
            DateTime creationDate = DateTime.Now;
            DateTime expiryDate = creationDate + TimeSpan.FromSeconds(tokenConfigurations.Seconds);
            var handler = new JwtSecurityTokenHandler();
            var securityToken = handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = tokenConfigurations.Issuer,
                Audience = tokenConfigurations.Audience,
                SigningCredentials = signingConfigurations.SigningCredentials,
                Subject = identity,
                NotBefore = creationDate,
                Expires = expiryDate
            });
            var token = handler.WriteToken(securityToken);
            return (creationDate, expiryDate, token);
        }
    }
}
