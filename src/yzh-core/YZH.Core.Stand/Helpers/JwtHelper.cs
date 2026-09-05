using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace YZH.Core.Stand.Helpers;

/// <summary>JWT 配置选项</summary>
public record JwtOptions
{
    public string Issuer { get; init; } = "yzh";
    public string Audience { get; init; } = "yzh-app";
    public string SecretKey { get; init; } = string.Empty;
    public int ExpirationMinutes { get; init; } = 43200; // 30天
}

/// <summary>JWT 工具 - 生成/解析/验证 Token</summary>
public class JwtHelper
{
    private readonly JwtOptions _options;

    public JwtHelper(IOptions<JwtOptions> options) : this(options.Value) { }
    public JwtHelper(JwtOptions options) { _options = options; }

    /// <summary>生成 JWT Token</summary>
    public string GenerateToken(int userId, string userName, int? roleId = null, TimeSpan? expiration = null)
    {
        var expireTime = DateTime.UtcNow.Add(expiration ?? TimeSpan.FromMinutes(_options.ExpirationMinutes));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, userId.ToString()),
            new(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new(JwtRegisteredClaimNames.Nbf, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            new(JwtRegisteredClaimNames.Exp, expireTime.Subtract(DateTime.UnixEpoch).TotalSeconds.ToString("F0")),
            new(JwtRegisteredClaimNames.Iss, _options.Issuer),
            new(JwtRegisteredClaimNames.Aud, _options.Audience),
            new(ClaimTypes.Name, userName),
            new(ClaimTypes.Role, roleId?.ToString() ?? "0")
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expireTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>解析 Token</summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            token = token?.Replace("Bearer ", "");
            return new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
                ClockSkew = TimeSpan.Zero
            }, out _);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>JWT 静态辅助工具（无需 DI）</summary>
public static class JwtHelperExtensions
{
    public static string? GetUserName(string token)
    {
        try
        {
            token = token?.Replace("Bearer ", "");
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Payload[ClaimTypes.Name]?.ToString();
        }
        catch { return null; }
    }

    public static int GetUserId(string token)
    {
        try
        {
            token = token?.Replace("Bearer ", "");
            return int.Parse(new JwtSecurityTokenHandler().ReadJwtToken(token).Id);
        }
        catch { return 0; }
    }

    public static bool IsExpired(string token)
    {
        try
        {
            token = token?.Replace("Bearer ", "");
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.ValidTo < DateTime.UtcNow;
        }
        catch { return true; }
    }
}
