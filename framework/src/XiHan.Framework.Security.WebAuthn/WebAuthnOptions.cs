// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.WebAuthn;

/// <summary>
/// 通行密钥依赖方的选项（配置节 <c>XiHan:Security:WebAuthn</c>）
/// </summary>
/// <remarks>
/// <see cref="RelyingPartyId"/> 与 <see cref="Origins"/> 必填；来源的主机必须等于 RP ID 或是它的子域，由 <see cref="WebAuthnCeremonies"/> 构造时核对
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record WebAuthnOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:Security:WebAuthn";

    /// <summary>
    /// 依赖方 ID
    /// </summary>
    /// <remarks>
    /// 可注册的域名（如 <c>example.com</c>、本地开发的 <c>localhost</c>），不带协议、端口与路径；保存为小写
    /// </remarks>
    public required string RelyingPartyId
    {
        get => field;
        init => field = IsDomain(value) ? value.ToLowerInvariant() : throw new ArgumentException("The relying party ID must be a domain name without scheme, port or path.", nameof(value));
    }

    /// <summary>
    /// 认证器上显示的依赖方名称
    /// </summary>
    /// <remarks>
    /// 为空时取 <see cref="RelyingPartyId"/>
    /// </remarks>
    public string? RelyingPartyName { get; init; }

    /// <summary>
    /// 允许的来源
    /// </summary>
    /// <remarks>
    /// 至少一个，形如 <c>scheme://host[:port]</c>、不带路径；<c>https</c> 之外只允许主机为 <c>localhost</c> 的 <c>http</c>。保存为规范形式（小写、省略缺省端口）
    /// </remarks>
    public required IReadOnlyList<string> Origins
    {
        get => field;
        init => field = NormalizeOrigins(value);
    }

    /// <summary>
    /// 对用户验证（指纹、面容、PIN）的要求
    /// </summary>
    /// <remarks>
    /// 默认 <see cref="WebAuthnRequirement.Required"/>：为 <see cref="WebAuthnRequirement.Required"/> 时核对响应带 UV 标志
    /// </remarks>
    public WebAuthnRequirement UserVerification { get; init; } = WebAuthnRequirement.Required;

    /// <summary>
    /// 对可发现凭据（免用户名登录）的要求
    /// </summary>
    /// <remarks>
    /// 默认 <see cref="WebAuthnRequirement.Required"/>
    /// </remarks>
    public WebAuthnRequirement ResidentKey { get; init; } = WebAuthnRequirement.Required;

    /// <summary>
    /// 仪式的超时
    /// </summary>
    /// <remarks>
    /// 默认 5 分钟，写进选项交给浏览器；应用保存挑战的时长不应短于它
    /// </remarks>
    public TimeSpan Timeout
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The ceremony timeout must be positive.");
    } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 挑战的字节数
    /// </summary>
    /// <remarks>
    /// 16 到 64，默认 32
    /// </remarks>
    public int ChallengeSize
    {
        get => field;
        init => field = value is >= 16 and <= 64 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The challenge size must be between 16 and 64 bytes.");
    } = 32;

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// <c>RelyingPartyId</c> 与 <c>Origins</c>（数组）必填；<c>UserVerification</c>、<c>ResidentKey</c> 取枚举名，<c>TimeoutSeconds</c> 与 <c>ChallengeSize</c> 是整数
    /// </remarks>
    /// <exception cref="ArgumentException">缺少必填项，或某个值不合法</exception>
    public static WebAuthnOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var origins = section.GetSection("Origins").GetChildren().Select(static child => child.Value).OfType<string>().ToList();
        var options = new WebAuthnOptions
        {
            RelyingPartyId = section["RelyingPartyId"] ?? throw new ArgumentException($"{ConfigurationSection}:RelyingPartyId is required."),
            RelyingPartyName = section["RelyingPartyName"] is { Length: > 0 } name ? name : null,
            Origins = origins.Count > 0 ? origins : throw new ArgumentException($"{ConfigurationSection}:Origins must list at least one origin."),
        };

        if (section["UserVerification"] is { } userVerification)
        {
            options = options with { UserVerification = ParseRequirement(userVerification, "UserVerification") };
        }

        if (section["ResidentKey"] is { } residentKey)
        {
            options = options with { ResidentKey = ParseRequirement(residentKey, "ResidentKey") };
        }

        if (section["TimeoutSeconds"] is { } timeout)
        {
            options = options with { Timeout = TimeSpan.FromSeconds(ParseInteger(timeout, "TimeoutSeconds")) };
        }

        if (section["ChallengeSize"] is { } challengeSize)
        {
            options = options with { ChallengeSize = ParseInteger(challengeSize, "ChallengeSize") };
        }

        // 启动时就暴露来源与 RP ID 不配套，而不是等到浏览器拒绝
        Validate(options);
        return options;
    }

    /// <summary>
    /// 核对来源与 RP ID 配套
    /// </summary>
    /// <exception cref="ArgumentException">某个来源的主机既不是 RP ID 也不是它的子域</exception>
    internal static void Validate(WebAuthnOptions options)
    {
        foreach (var origin in options.Origins)
        {
            var host = new Uri(origin, UriKind.Absolute).Host;
            if (!string.Equals(host, options.RelyingPartyId, StringComparison.Ordinal) && !host.EndsWith("." + options.RelyingPartyId, StringComparison.Ordinal))
            {
                throw new ArgumentException($"The origin '{origin}' is not within the relying party ID '{options.RelyingPartyId}'.", nameof(options));
            }
        }
    }

    /// <summary>
    /// 是否是不带协议、端口与路径的域名
    /// </summary>
    private static bool IsDomain(string? value)
        => !string.IsNullOrWhiteSpace(value) && value.Length <= 253 && Uri.CheckHostName(value) == UriHostNameType.Dns && !value.EndsWith('.');

    /// <summary>
    /// 来源规范化为 <c>scheme://host[:port]</c>
    /// </summary>
    private static List<string> NormalizeOrigins(IReadOnlyList<string>? values)
    {
        if (values is null || values.Count == 0)
        {
            throw new ArgumentException("At least one origin is required.", nameof(values));
        }

        var origins = new List<string>(values.Count);
        foreach (var value in values)
        {
            // 来源只是协议、主机与端口：带路径、查询或片段的多半是把页面地址误填进来
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.UserInfo.Length > 0)
            {
                throw new ArgumentException($"The origin '{value}' must be 'scheme://host[:port]' without a path.", nameof(values));
            }

            var secure = uri.Scheme == Uri.UriSchemeHttps;
            var localhost = uri.Scheme == Uri.UriSchemeHttp && string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
            if (!secure && !localhost)
            {
                throw new ArgumentException($"The origin '{value}' must use https; plain http is allowed only for localhost.", nameof(values));
            }

            var origin = uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
            if (!origins.Contains(origin))
            {
                origins.Add(origin);
            }
        }

        return origins;
    }

    /// <summary>
    /// 解析要求程度
    /// </summary>
    private static WebAuthnRequirement ParseRequirement(string value, string key)
        => Enum.TryParse<WebAuthnRequirement>(value, ignoreCase: true, out var requirement) && Enum.IsDefined(requirement) && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
            ? requirement
            : throw new ArgumentException($"{ConfigurationSection}:{key} must be Required, Preferred or Discouraged, but was '{value}'.");

    /// <summary>
    /// 解析整数
    /// </summary>
    private static int ParseInteger(string value, string key)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : throw new ArgumentException($"{ConfigurationSection}:{key} must be an integer, but was '{value}'.");
}
