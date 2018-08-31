using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using RestServer.Infrastructure.AspNetCore;

namespace RestServer.Util
{
    internal static class AuthenticationUtils
    {
        public static async Task<(DateTime creationDate, DateTime expiryDate, string token)> GenerateJwtTokenForUser(string userId, TokenConfigurations tokenConfigurations, SigningConfigurations signingConfigurations)
        {
            ClaimsIdentity identity = new ClaimsIdentity
            (
                new GenericIdentity(userId, "Login")//,
                // JWT Token was too heavy: We're only keeping ID and the random seed now. Still heavy though
                // TODO: Make it lighter
                //new[]
                //{
                    // new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                    // new Claim(JwtRegisteredClaimNames.UniqueName, userId),
                    // new Claim(JwtRegisteredClaimNames.Email, email),
                //}
            );
            DateTime creationDate = DateTime.Now;
            DateTime expiryDate = creationDate + TimeSpan.FromSeconds(tokenConfigurations.Seconds);
            var handler = new JwtSecurityTokenHandler();
            var securityToken = await Task.Run(() => handler.CreateToken
            (
                new SecurityTokenDescriptor
                {
                    Issuer = tokenConfigurations.Issuer,
                    Audience = tokenConfigurations.Audience,
                    SigningCredentials = signingConfigurations.SigningCredentials,
                    Subject = identity,
                    NotBefore = creationDate,
                    Expires = expiryDate,
                    IssuedAt = creationDate,
                })
            );
            var token = handler.WriteToken(securityToken);
            return (creationDate, expiryDate, token);
        }
    }
}
