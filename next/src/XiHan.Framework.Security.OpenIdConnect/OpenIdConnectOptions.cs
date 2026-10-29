// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.OpenIdConnect;

/// <summary>
/// OpenID Connect 认证选项（配置节 <c>XiHan:Security:OpenIdConnect</c>）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record OpenIdConnectOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Security:OpenIdConnect";

    /// <summary>
    /// 发现文档相对签发者地址的路径。
    /// </summary>
    public const string DiscoveryPath = ".well-known/openid-configuration";

    /// <summary>
    /// 签发者地址（绝对 URI，如 <c>https://login.example.com/realms/shop</c>）。
    /// </summary>
    /// <remarks>
    /// 发现文档默认在其下的 <see cref="DiscoveryPath"/>。
    /// </remarks>
    public required string Authority
    {
        get => field;
        init => field = IsHttpUri(value) ? value : throw new ArgumentException("The authority must be an absolute http or https URI.", nameof(value));
    }

    /// <summary>
    /// 发现文档地址。
    /// </summary>
    /// <remarks>
    /// 为空时由 <see cref="Authority"/> 推出。
    /// </remarks>
    public string? MetadataAddress
    {
        get => field;
        init => field = value is null || IsHttpUri(value) ? value : throw new ArgumentException("The metadata address must be an absolute http or https URI.", nameof(value));
    }

    /// <summary>
    /// 期望的签发者。
    /// </summary>
    /// <remarks>
    /// 为空时取发现文档里的 <c>issuer</c>。
    /// </remarks>
    public string? Issuer { get; init; }

    /// <summary>
    /// 期望的受众。
    /// </summary>
    /// <remarks>
    /// 为空时不校验受众。
    /// </remarks>
    public string? Audience { get; init; }

    /// <summary>
    /// 发现文档与 JWKS 是否必须经 HTTPS 取得。
    /// </summary>
    /// <remarks>
    /// 默认是，只在本地开发时关闭。
    /// </remarks>
    public bool RequireHttpsMetadata { get; init; } = true;

    /// <summary>
    /// 校验有效期时允许的时钟偏移。
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
    /// 元数据的自动刷新间隔。
    /// </summary>
    /// <remarks>
    /// 默认 12 小时。
    /// </remarks>
    public TimeSpan RefreshInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The refresh interval must be positive.");
    } = TimeSpan.FromHours(12);

    /// <summary>
    /// 两次按需刷新（未知签名密钥触发）之间的最短间隔，也是刷新失败后沿用旧密钥的时长。
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟。
    /// </remarks>
    public TimeSpan MinimumRefreshInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The minimum refresh interval must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 取发现文档或 JWKS 的 HTTP 超时。
    /// </summary>
    /// <remarks>
    /// 默认 30 秒。
    /// </remarks>
    public TimeSpan BackchannelTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The backchannel timeout must be positive.");
    } = TimeSpan.FromSeconds(30);

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
    /// 发现文档的最终地址：<see cref="MetadataAddress"/>，否则 <see cref="Authority"/> 下的 <see cref="DiscoveryPath"/>。
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="RequireHttpsMetadata"/> 为真而地址不是 HTTPS。</exception>
    public Uri ResolveMetadataAddress()
    {
        var address = MetadataAddress is { } explicitAddress
            ? new Uri(explicitAddress, UriKind.Absolute)
            : new Uri(Authority.EndsWith('/') ? Authority + DiscoveryPath : Authority + "/" + DiscoveryPath, UriKind.Absolute);
        return !RequireHttpsMetadata || address.Scheme == Uri.UriSchemeHttps
            ? address
            : throw new InvalidOperationException($"The OpenID Connect metadata address '{address}' must use HTTPS; set {ConfigurationSection}:RequireHttpsMetadata to false only for local development.");
    }

    /// <summary>
    /// 从配置读取。
    /// </summary>
    /// <remarks>
    /// <c>Authority</c> 必填，各时长以秒为单位（<c>ClockSkewSeconds</c>、<c>RefreshIntervalSeconds</c>、<c>MinimumRefreshIntervalSeconds</c>、<c>BackchannelTimeoutSeconds</c>）。
    /// </remarks>
    public static OpenIdConnectOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new OpenIdConnectOptions
        {
            Authority = configuration[$"{ConfigurationSection}:Authority"] ?? throw new ArgumentException($"{ConfigurationSection}:Authority is required."),
            MetadataAddress = configuration[$"{ConfigurationSection}:MetadataAddress"],
            Issuer = configuration[$"{ConfigurationSection}:Issuer"],
            Audience = configuration[$"{ConfigurationSection}:Audience"],
        };

        if (configuration[$"{ConfigurationSection}:RequireHttpsMetadata"] is { } requireHttps)
        {
            options = bool.TryParse(requireHttps, out var value)
                ? options with { RequireHttpsMetadata = value }
                : throw new ArgumentException($"{ConfigurationSection}:RequireHttpsMetadata must be true or false, but was '{requireHttps}'.");
        }

        if (Seconds(configuration, "ClockSkewSeconds") is { } clockSkew)
        {
            options = options with { ClockSkew = clockSkew };
        }

        if (Seconds(configuration, "RefreshIntervalSeconds") is { } refresh)
        {
            options = options with { RefreshInterval = refresh };
        }

        if (Seconds(configuration, "MinimumRefreshIntervalSeconds") is { } minimum)
        {
            options = options with { MinimumRefreshInterval = minimum };
        }

        if (Seconds(configuration, "BackchannelTimeoutSeconds") is { } timeout)
        {
            options = options with { BackchannelTimeout = timeout };
        }

        if (configuration[$"{ConfigurationSection}:NameClaim"] is { } nameClaim)
        {
            options = options with { NameClaim = nameClaim };
        }

        // 启动时就暴露地址与 HTTPS 要求的冲突，而不是等到第一次认证
        _ = options.ResolveMetadataAddress();
        return options;
    }

    /// <summary>
    /// 读取以秒为单位的时长。
    /// </summary>
    /// <remarks>
    /// 未配置为空，不是非负数则抛出。
    /// </remarks>
    private static TimeSpan? Seconds(IConfiguration configuration, string name)
    {
        var text = configuration[$"{ConfigurationSection}:{name}"];
        if (text is null)
        {
            return null;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0
            ? TimeSpan.FromSeconds(seconds)
            : throw new ArgumentException($"{ConfigurationSection}:{name} must be a non-negative number, but was '{text}'.");
    }

    /// <summary>
    /// 是否为绝对的 http / https URI。
    /// </summary>
    private static bool IsHttpUri(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
