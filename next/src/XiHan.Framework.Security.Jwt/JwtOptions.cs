// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Jwt;

/// <summary>
/// JWT 选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:Security:Jwt</c> 显式读取。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record JwtOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Security:Jwt";

    /// <summary>
    /// 对称密钥最小字节数（HS256）。
    /// </summary>
    public const int MinSecretBytes = 32;

    /// <summary>
    /// 对称密钥。
    /// </summary>
    public required string Secret
    {
        get => field;
        init => field = value is not null && Encoding.UTF8.GetByteCount(value) >= MinSecretBytes
            ? value
            : throw new ArgumentException($"The JWT secret must be at least {MinSecretBytes} bytes long.", nameof(value));
    }

    /// <summary>
    /// 签发者。
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 不校验。
    /// </remarks>
    public string? Issuer { get; init; }

    /// <summary>
    /// 受众。
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> 不校验。
    /// </remarks>
    public string? Audience { get; init; }

    /// <summary>
    /// 时钟偏移。
    /// </summary>
    /// <remarks>
    /// 默认 60 秒。
    /// </remarks>
    public TimeSpan ClockSkew
    {
        get => field;
        init => field = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The clock skew must not be negative.");
    } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 作为主体 ID 的声明类型。
    /// </summary>
    /// <remarks>
    /// 默认 <c>sub</c>。
    /// </remarks>
    public string NameClaim
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The subject claim type must not be empty.", nameof(value));
    } = "sub";

    /// <summary>
    /// 从配置读取：<c>Secret</c>（必填）、<c>Issuer</c>、<c>Audience</c>、<c>ClockSkewSeconds</c>、<c>NameClaim</c>。
    /// </summary>
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new JwtOptions
        {
            Secret = configuration[$"{ConfigurationSection}:Secret"] ?? throw new ArgumentException($"{ConfigurationSection}:Secret is required."),
            Issuer = configuration[$"{ConfigurationSection}:Issuer"],
            Audience = configuration[$"{ConfigurationSection}:Audience"],
        };

        var skew = configuration[$"{ConfigurationSection}:ClockSkewSeconds"];
        if (skew is not null)
        {
            options = double.TryParse(skew, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
                ? options with { ClockSkew = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:ClockSkewSeconds must be a non-negative number, but was '{skew}'.");
        }

        if (configuration[$"{ConfigurationSection}:NameClaim"] is { } nameClaim)
        {
            options = options with { NameClaim = nameClaim };
        }

        return options;
    }
}
