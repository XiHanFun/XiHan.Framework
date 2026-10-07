// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using XiHan.Framework.Authentication.Jwt;

namespace XiHan.Framework.Authentication.Tests.Jwt;

public class JwtTokenServiceConcurrencyTests
{
    [Fact]
    public async Task RefreshAccessToken_ConcurrentRequests_ConsumesRefreshTokenOnce()
    {
        using var validationBarrier = new Barrier(2);
        var store = new CoordinatedRefreshTokenStore(validationBarrier);
        const string secret = "a sufficiently long test secret key for HMAC";
        var service = new JwtTokenService(Options.Create(new JwtOptions
        {
            SecretKey = secret,
            ValidateIssuer = false,
            ValidateAudience = false
        }), store);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "user-1") };
        var handler = new JwtSecurityTokenHandler();
        var expiredJwt = handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = DateTime.UtcNow.AddHours(-2),
            Expires = DateTime.UtcNow.AddHours(-1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256Signature)
        });
        var expiredAccessToken = handler.WriteToken(expiredJwt);
        store.Save("refresh-token", "user-1", DateTime.UtcNow.AddDays(1));

        var results = await Task.WhenAll(
            Task.Run(() => service.RefreshAccessToken(expiredAccessToken, "refresh-token")),
            Task.Run(() => service.RefreshAccessToken(expiredAccessToken, "refresh-token")));

        Assert.Single(results, result => result is not null);
    }

    private sealed class CoordinatedRefreshTokenStore(Barrier validationBarrier) : IRefreshTokenStore
    {
        private readonly ConcurrentDictionary<string, (string? Subject, DateTime ExpiresAt)> _tokens = new();

        public void Save(string refreshToken, string? subject, DateTime expiresAt) =>
            _tokens[refreshToken] = (subject, expiresAt);

        public bool Validate(string refreshToken, string? subject = null)
        {
            var valid = _tokens.TryGetValue(refreshToken, out var entry) &&
                entry.ExpiresAt > DateTime.UtcNow &&
                (string.IsNullOrWhiteSpace(subject) || string.Equals(entry.Subject, subject, StringComparison.Ordinal));
            validationBarrier.SignalAndWait();
            return valid;
        }

        public bool TryConsume(string refreshToken, string? subject = null)
        {
            lock (_tokens)
            {
                if (!ValidateWithoutBarrier(refreshToken, subject))
                {
                    return false;
                }

                return _tokens.TryRemove(refreshToken, out _);
            }
        }

        private bool ValidateWithoutBarrier(string refreshToken, string? subject) =>
            _tokens.TryGetValue(refreshToken, out var entry) &&
            entry.ExpiresAt > DateTime.UtcNow &&
            (string.IsNullOrWhiteSpace(subject) || string.Equals(entry.Subject, subject, StringComparison.Ordinal));

        public void Remove(string refreshToken) => _tokens.TryRemove(refreshToken, out _);
    }
}
