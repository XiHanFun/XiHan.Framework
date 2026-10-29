// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Configuration.Nacos;

/// <summary>
/// Nacos Provider 的选项。
/// </summary>
/// <remarks>
/// 配置节 <c>XiHan:Configuration:Nacos</c>：<c>ServerAddresses</c>（逗号或分号分隔，或绑定 <c>nacos</c> 的值）、<c>Group</c>、<c>Tenant</c>（Nacos 命名空间 ID）、<c>Username</c> / <c>Password</c>、<c>Format</c>、<c>LongPollTimeoutSeconds</c>、<c>RequestTimeoutSeconds</c>。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record NacosOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:Configuration:Nacos";

    /// <summary>
    /// 服务地址（绝对的 http / https URL），按序尝试。
    /// </summary>
    /// <remarks>
    /// 至少一个。
    /// </remarks>
    public required IReadOnlyList<string> ServerAddresses
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Count == 0)
            {
                throw new ArgumentException("At least one Nacos server address is required.", nameof(value));
            }

            foreach (var address in value)
            {
                if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new ArgumentException($"The Nacos server address '{address}' must be an absolute http or https URL.", nameof(value));
                }
            }

            field = value;
        }
    }

    /// <summary>
    /// 配置分组。
    /// </summary>
    /// <remarks>
    /// 默认 <c>DEFAULT_GROUP</c>。
    /// </remarks>
    public string Group
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The group must not be empty.", nameof(value));
    } = "DEFAULT_GROUP";

    /// <summary>
    /// Nacos 命名空间 ID（租户）。
    /// </summary>
    /// <remarks>
    /// 空表示公共命名空间。
    /// </remarks>
    public string Tenant { get; init; } = string.Empty;

    /// <summary>
    /// 登录用户名。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Password"/> 一起给出时先登录取令牌。
    /// </remarks>
    public string? Username { get; init; }

    /// <summary>
    /// 登录密码。
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// 配置内容的格式。
    /// </summary>
    /// <remarks>
    /// 默认 JSON。
    /// </remarks>
    public NacosConfigFormat Format { get; init; } = NacosConfigFormat.Json;

    /// <summary>
    /// 长轮询窗口。
    /// </summary>
    /// <remarks>
    /// 默认 30 秒。
    /// </remarks>
    public TimeSpan LongPollTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The long poll timeout must be positive.");
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 单次请求（取文档、登录）的超时。
    /// </summary>
    /// <remarks>
    /// 默认 10 秒。
    /// </remarks>
    public TimeSpan RequestTimeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The request timeout must be positive.");
    } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// 从配置与绑定读取（地址：配置优先于绑定值）。
    /// </summary>
    public static NacosOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var addresses = configuration[$"{ConfigurationSection}:ServerAddresses"]
            ?? (binding.TryGetValue(out var bound) ? bound.Value : null)
            ?? throw new ArgumentException($"No Nacos server address is configured: set {ConfigurationSection}:ServerAddresses or provide the 'nacos' binding.");
        var options = new NacosOptions
        {
            ServerAddresses = [.. addresses.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)],
            Username = Text(configuration, "Username"),
            Password = Text(configuration, "Password"),
            Tenant = Text(configuration, "Tenant") ?? string.Empty,
        };
        if (Text(configuration, "Group") is { } group)
        {
            options = options with { Group = group };
        }

        if (Text(configuration, "Format") is { } format)
        {
            options = Enum.TryParse<NacosConfigFormat>(format, ignoreCase: true, out var parsed)
                ? options with { Format = parsed }
                : throw new ArgumentException($"{ConfigurationSection}:Format must be 'Json' or 'Properties', but was '{format}'.");
        }

        if (Text(configuration, "LongPollTimeoutSeconds") is { } longPoll)
        {
            options = options with { LongPollTimeout = Seconds(longPoll, "LongPollTimeoutSeconds") };
        }

        if (Text(configuration, "RequestTimeoutSeconds") is { } request)
        {
            options = options with { RequestTimeout = Seconds(request, "RequestTimeoutSeconds") };
        }

        return options;
    }

    /// <summary>
    /// 读一个非空文本值。
    /// </summary>
    /// <remarks>
    /// 缺失或空白为 <see langword="null"/>。
    /// </remarks>
    private static string? Text(IConfiguration configuration, string name)
        => configuration[$"{ConfigurationSection}:{name}"] is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// 解析正数秒。
    /// </summary>
    /// <remarks>
    /// 不合法抛出。
    /// </remarks>
    private static TimeSpan Seconds(string text, string name)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0
            ? TimeSpan.FromSeconds(value)
            : throw new ArgumentException($"{ConfigurationSection}:{name} must be a positive number, but was '{text}'.");
}
