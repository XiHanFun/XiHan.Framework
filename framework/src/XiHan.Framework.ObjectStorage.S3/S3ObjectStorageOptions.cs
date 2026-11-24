// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// S3 对象存储选项
/// </summary>
/// <remarks>
/// <para>从配置节 <c>XiHan:ObjectStorage:S3</c> 显式读取：<c>ServiceUrl</c>、<c>Region</c>、<c>AccessKey</c>、<c>SecretKey</c>、<c>SessionToken</c>、<c>ForcePathStyle</c>、<c>EnsureBuckets</c>、<c>HealthBucket</c>、<c>ConditionalWrites</c>、<c>HealthProbeIntervalSeconds</c>，以及客户端的设置 <c>RequestTimeoutSeconds</c>、<c>MaxAttempts</c>、<c>ProxyHost</c>、<c>ProxyPort</c>、<c>ProxyUserName</c>、<c>ProxyPassword</c>（ADR-0281）；绑定 <c>objectstorage</c> 存在时其主值与属性（<c>region</c>、<c>accessKey</c>、<c>secretKey</c>、<c>sessionToken</c>、<c>forcePathStyle</c>）逐项覆盖配置，空白的不算给出（RFC-0016「资源绑定与配置节」）。命名实例（Provider <c>s3</c>）的设置用同样的键（不含 <c>HealthProbeIntervalSeconds</c>）。</para>
/// <para>成组的设置必须给全：会话令牌要有两项密钥，代理的主机与端口、用户名与口令各自成对，用户名与口令只能跟着代理给。<see cref="FromConfiguration(IConfiguration)"/> 与 <see cref="FromSettings"/> 读完即核对，只给了一部分抛出 <see cref="ArgumentException"/>；消息里只写键名，不带令牌、主机、用户名与口令的值。</para>
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record S3ObjectStorageOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:ObjectStorage:S3";

    /// <summary>
    /// 服务端点（如 <c>http://localhost:9000</c>、<c>https://oss-cn-hangzhou.aliyuncs.com</c>）
    /// </summary>
    /// <remarks>
    /// 空表示按 <see cref="Region"/> 访问 Amazon S3
    /// </remarks>
    public string? ServiceUrl
    {
        get => field;
        init => field = value is null || Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? value
            : throw new ArgumentException($"The service URL '{value}' must be an absolute http or https URL.", nameof(value));
    }

    /// <summary>
    /// 区域
    /// </summary>
    /// <remarks>
    /// 访问 Amazon S3 时选端点，访问兼容服务时作签名区域。默认 <c>us-east-1</c>
    /// </remarks>
    public string Region
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The region must not be empty.", nameof(value));
    } = "us-east-1";

    /// <summary>
    /// 访问密钥 ID
    /// </summary>
    /// <remarks>
    /// 与 <see cref="SecretKey"/> 都为空时用 AWS SDK 的默认凭据链（环境变量、配置文件、实例角色）。是访问密钥的一半，打印选项时与 <see cref="SecretKey"/> 一样遮蔽（ADR-0089）
    /// </remarks>
    public string? AccessKey { get; init; }

    /// <summary>
    /// 访问密钥
    /// </summary>
    public string? SecretKey { get; init; }

    /// <summary>
    /// 临时凭据的会话令牌
    /// </summary>
    /// <remarks>
    /// 必须与 <see cref="AccessKey"/>、<see cref="SecretKey"/> 同时给出：只有令牌没有密钥时不退回凭据链，读取选项时抛出。是凭据，打印选项时遮蔽（ADR-0281）
    /// </remarks>
    public string? SessionToken { get; init; }

    /// <summary>
    /// 桶名放在路径而不是主机名里
    /// </summary>
    /// <remarks>
    /// 兼容服务多数只支持路径式。默认开启
    /// </remarks>
    public bool ForcePathStyle { get; init; } = true;

    /// <summary>
    /// 写入时桶不存在就创建
    /// </summary>
    /// <remarks>
    /// 默认关闭，桶由基础设施预先创建
    /// </remarks>
    public bool EnsureBuckets { get; init; }

    /// <summary>
    /// 框架探测用的桶
    /// </summary>
    /// <remarks>
    /// 健康项 <c>objectstorage.s3.connection</c> 与命名实例的探测对它 HEAD（同时验证凭据对该桶的权限），为 <see langword="null"/> 时列桶；<see cref="ConditionalWrites"/> 开启时启动、建实例与后台重试时在其中写探测键 <c>.xihan/probe/conditional-write</c>，这时必须配置（ADR-0089、ADR-0090）
    /// </remarks>
    public string? HealthBucket { get; init; }

    /// <summary>
    /// 不覆盖写入是否由条件写入保证
    /// </summary>
    /// <remarks>
    /// 默认开启：缺省实例启动时、命名实例建出时在 <see cref="HealthBucket"/> 里探测服务是否认 <c>If-None-Match: *</c>。探明不认、凭据被拒或探测桶不存在即启动失败或不建实例；只有连不上是尚未确认，照常启动、在后台重试，探明之前 <c>Overwrite = false</c> 的写入返回 <c>OBJECTSTORAGE_UNAVAILABLE_004</c>——任何时候都不带着可能静默覆盖的不覆盖写入运行（ADR-0089、ADR-0090、ADR-0093）。关闭表示应用明确不需要不覆盖写入：不探测、<see cref="HealthBucket"/> 可以不配，<c>Overwrite = false</c> 的写入与分片上传一律返回 <c>OBJECTSTORAGE_UNAVAILABLE_003</c>，不访问存储
    /// </remarks>
    public bool ConditionalWrites { get; init; } = true;

    /// <summary>
    /// 健康项 <c>objectstorage.s3.connection</c> 的重探间隔（也是单次探测的超时）
    /// </summary>
    /// <remarks>
    /// 默认 15 秒。条件写入尚未确认时后台重试的间隔从 1 秒起倍增，最长也是它（ADR-0090）
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 请求超时的上限：24 小时
    /// </summary>
    public static readonly TimeSpan MaxRequestTimeout = TimeSpan.FromHours(24);

    /// <summary>
    /// 尝试次数的上限
    /// </summary>
    public const int MaxAttemptsLimit = 10;

    /// <summary>
    /// 单次 HTTP 请求的超时
    /// </summary>
    /// <remarks>
    /// 每次尝试各算各的，交给 SDK 作底层 <see cref="HttpClient.Timeout"/>；正的时长，不超过 <see cref="MaxRequestTimeout"/>。为 <see langword="null"/>（缺省）时不另设时限，沿用 SDK 对 S3 的缺省（约 24.8 天）。超时与其余网络错误一样映射为 <c>OBJECTSTORAGE_UNAVAILABLE_001</c>（ADR-0281）
    /// </remarks>
    public TimeSpan? RequestTimeout
    {
        get => field;
        init => field = value is null || (value > TimeSpan.Zero && value <= MaxRequestTimeout)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"The request timeout must be positive and at most {MaxRequestTimeout.TotalSeconds.ToString(CultureInfo.InvariantCulture)} seconds.");
    }

    /// <summary>
    /// 一次操作最多发出的请求数，含首次
    /// </summary>
    /// <remarks>
    /// 1 到 <see cref="MaxAttemptsLimit"/>，1 即不重试。为 <see langword="null"/>（缺省）时沿用 SDK 的缺省（标准重试模式，共 3 次；ADR-0281）
    /// </remarks>
    public int? MaxAttempts
    {
        get => field;
        init => field = value is null or (>= 1 and <= MaxAttemptsLimit)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"The maximum number of attempts must be between 1 and {MaxAttemptsLimit.ToString(CultureInfo.InvariantCulture)}.");
    }

    /// <summary>
    /// HTTP 代理的主机
    /// </summary>
    /// <remarks>
    /// 主机名或 IP 字面量，不带协议、端口、路径与用户信息；必须与 <see cref="ProxyPort"/> 同时给出。不合法时抛出的消息不带原值：写错成带用户信息的地址时口令不会进日志（ADR-0281）
    /// </remarks>
    public string? ProxyHost
    {
        get => field;
        init => field = value is null || IsProxyHost(value)
            ? value
            : throw new ArgumentException("The proxy host must be a host name or an IP address literal, without a scheme, port, path or user information.", nameof(value));
    }

    /// <summary>
    /// HTTP 代理的端口
    /// </summary>
    /// <remarks>
    /// 1 到 65535；必须与 <see cref="ProxyHost"/> 同时给出
    /// </remarks>
    public int? ProxyPort
    {
        get => field;
        init => field = value is null or (>= 1 and <= MaxPort)
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, $"The proxy port must be between 1 and {MaxPort.ToString(CultureInfo.InvariantCulture)}.");
    }

    /// <summary>
    /// 代理的用户名
    /// </summary>
    /// <remarks>
    /// 可选；必须与 <see cref="ProxyPassword"/> 同时给出，且只能跟着代理给。是登录凭据的身份那一半，打印选项时遮蔽（RFC-0014 规则 2）
    /// </remarks>
    public string? ProxyUserName { get; init; }

    /// <summary>
    /// 代理的口令
    /// </summary>
    /// <remarks>
    /// 可选；必须与 <see cref="ProxyUserName"/> 同时给出。是凭据，打印选项时遮蔽
    /// </remarks>
    public string? ProxyPassword { get; init; }

    /// <summary>
    /// 端口号的上限
    /// </summary>
    private const int MaxPort = 65535;

    /// <summary>
    /// 命名实例的设置键
    /// </summary>
    /// <remarks>
    /// 探测间隔由注册表统一（<c>XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds</c>），不在其中
    /// </remarks>
    private static readonly string[] _settingKeys =
    [
        "ServiceUrl", "Region", "AccessKey", "SecretKey", "SessionToken", "ForcePathStyle", "EnsureBuckets", "HealthBucket", "ConditionalWrites",
        "RequestTimeoutSeconds", "MaxAttempts", "ProxyHost", "ProxyPort", "ProxyUserName", "ProxyPassword",
    ];

    /// <summary>
    /// 从配置读取，再用绑定（若有）覆盖
    /// </summary>
    /// <remarks>
    /// <c>binding</c> 取 <c>default</c>
    /// </remarks>
    public static S3ObjectStorageOptions FromConfiguration(IConfiguration configuration)
        => FromConfiguration(configuration, default);

    /// <summary>
    /// 从配置读取，再用绑定（若有）覆盖
    /// </summary>
    public static S3ObjectStorageOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = Parse(name => configuration[$"{ConfigurationSection}:{name}"], ConfigurationSection + ":");
        if (ConfigurationValues.ReadSeconds(configuration.GetSection(ConfigurationSection), "HealthProbeIntervalSeconds") is { } probe)
        {
            options = options with { HealthProbeInterval = probe };
        }

        // 绑定来自部署环境（Service Binding），给出的地址与凭据逐项覆盖应用配置；空白的主值与属性不算给出，照读配置节
        if (binding.TryGetValue(out var value))
        {
            if (!string.IsNullOrWhiteSpace(value.Value))
            {
                options = options with { ServiceUrl = value.Value };
            }

            if (BoundText(value, "region") is { } boundRegion)
            {
                options = options with { Region = boundRegion };
            }

            if (BoundText(value, "accessKey") is { } accessKey)
            {
                options = options with { AccessKey = accessKey };
            }

            if (BoundText(value, "secretKey") is { } secretKey)
            {
                options = options with { SecretKey = secretKey };
            }

            if (BoundText(value, "sessionToken") is { } sessionToken)
            {
                options = options with { SessionToken = sessionToken };
            }

            if (BoundText(value, "forcePathStyle") is { } boundPathStyle)
            {
                options = options with { ForcePathStyle = ParseBoolean(boundPathStyle, "The binding property forcePathStyle") };
            }
        }

        // 绑定覆盖之后再核对成组的设置：令牌与密钥可以一半来自绑定、一半来自配置节
        return options.RequireComplete(ConfigurationSection + ":");
    }

    /// <summary>
    /// 读绑定的一个非空属性
    /// </summary>
    /// <remarks>
    /// 没有该属性或属性空白为 <see langword="null"/>
    /// </remarks>
    private static string? BoundText(BindingValue binding, string key)
        => binding.Property(key).TryGetValue(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    /// <summary>
    /// 从命名实例的设置读取
    /// </summary>
    /// <remarks>
    /// 键同配置节，不区分大小写；没有绑定覆盖。未知的键抛出
    /// </remarks>
    public static S3ObjectStorageOptions FromSettings(IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var lookup = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
        if (lookup.Keys.FirstOrDefault(static key => !_settingKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) is { } unknown)
        {
            throw new ArgumentException($"Unknown S3 setting '{unknown}'; the known settings are {string.Join(", ", _settingKeys)}.", nameof(settings));
        }

        return Parse(name => lookup.TryGetValue(name, out var value) ? value : null, string.Empty).RequireComplete(string.Empty);
    }

    /// <summary>
    /// 核对成组的设置都给全了
    /// </summary>
    /// <remarks>
    /// 会话令牌要有两项密钥；代理的主机与端口成对，用户名与口令成对且只能跟着代理给。只给一部分时照着用，要么悄悄退回凭据链、要么悄悄不走代理，所以一律拒绝。消息只写键名（<paramref name="prefix"/> 是键的前缀），不带任何值
    /// </remarks>
    /// <exception cref="ArgumentException">成组的设置只给了一部分</exception>
    internal S3ObjectStorageOptions RequireComplete(string prefix)
    {
        if (SessionToken is not null && (AccessKey is null || SecretKey is null))
        {
            throw new ArgumentException($"'{prefix}SessionToken' requires both '{prefix}AccessKey' and '{prefix}SecretKey'.");
        }

        if ((ProxyHost is null) != (ProxyPort is null))
        {
            throw new ArgumentException($"'{prefix}ProxyHost' and '{prefix}ProxyPort' must be set together.");
        }

        if ((ProxyUserName is null) != (ProxyPassword is null))
        {
            throw new ArgumentException($"'{prefix}ProxyUserName' and '{prefix}ProxyPassword' must be set together.");
        }

        if (ProxyUserName is not null && ProxyHost is null)
        {
            throw new ArgumentException($"'{prefix}ProxyUserName' and '{prefix}ProxyPassword' require '{prefix}ProxyHost' and '{prefix}ProxyPort'.");
        }

        return this;
    }

    /// <summary>
    /// 按名读取连接与行为各项（不含探测间隔）
    /// </summary>
    /// <remarks>
    /// <paramref name="prefix"/> 只用于报错
    /// </remarks>
    private static S3ObjectStorageOptions Parse(Func<string, string?> read, string prefix)
    {
        var options = new S3ObjectStorageOptions
        {
            ServiceUrl = Text(read, "ServiceUrl"),
            AccessKey = Text(read, "AccessKey"),
            SecretKey = Text(read, "SecretKey"),
            SessionToken = Text(read, "SessionToken"),
            HealthBucket = Text(read, "HealthBucket"),
            ProxyUserName = Text(read, "ProxyUserName"),
            ProxyPassword = Text(read, "ProxyPassword"),
        };
        if (Text(read, "Region") is { } region)
        {
            options = options with { Region = region };
        }

        if (read("ForcePathStyle") is { } pathStyle)
        {
            options = options with { ForcePathStyle = ParseBoolean(pathStyle, prefix + "ForcePathStyle") };
        }

        if (read("EnsureBuckets") is { } ensure)
        {
            options = options with { EnsureBuckets = ParseBoolean(ensure, prefix + "EnsureBuckets") };
        }

        if (read("ConditionalWrites") is { } conditional)
        {
            options = options with { ConditionalWrites = ParseBoolean(conditional, prefix + "ConditionalWrites") };
        }

        if (read("RequestTimeoutSeconds") is { } timeout)
        {
            options = options with { RequestTimeout = ParseSeconds(timeout, prefix + "RequestTimeoutSeconds", MaxRequestTimeout) };
        }

        if (read("MaxAttempts") is { } attempts)
        {
            options = options with { MaxAttempts = ParseInt32(attempts, prefix + "MaxAttempts", 1, MaxAttemptsLimit) };
        }

        if (Text(read, "ProxyHost") is { } proxyHost)
        {
            // 先判再赋值，报错带键名而不带原值：主机写成带用户信息的地址时口令不能出现在消息里
            options = IsProxyHost(proxyHost)
                ? options with { ProxyHost = proxyHost }
                : throw new ArgumentException($"'{prefix}ProxyHost' must be a host name or an IP address literal, without a scheme, port, path or user information.");
        }

        if (read("ProxyPort") is { } proxyPort)
        {
            options = options with { ProxyPort = ParseInt32(proxyPort, prefix + "ProxyPort", 1, MaxPort) };
        }

        return options;
    }

    /// <summary>
    /// 读一个非空文本值
    /// </summary>
    /// <remarks>
    /// 缺失或空白为 <see langword="null"/>
    /// </remarks>
    private static string? Text(Func<string, string?> read, string name)
        => read(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// 解析布尔值
    /// </summary>
    /// <remarks>
    /// 不是 <c>true</c> / <c>false</c> 时抛出，<paramref name="name"/> 是报错用的全名
    /// </remarks>
    private static bool ParseBoolean(string text, string name)
        => bool.TryParse(text, out var value) ? value : throw new ArgumentException($"{name} must be 'true' or 'false', but was '{text}'.");

    /// <summary>
    /// 是否是代理主机的合法写法
    /// </summary>
    /// <remarks>
    /// 主机名或 IP 字面量：带协议、端口、路径、用户信息或空白的都不是。带区域标识的 IPv6 地址（<c>fe80::1%eth0</c>）也不收：拼成代理地址时区域会被悄悄丢掉，连到的不是写的那个地址
    /// </remarks>
    private static bool IsProxyHost(string value)
        => Uri.CheckHostName(value) is (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6) && !value.Contains('%', StringComparison.Ordinal);

    /// <summary>
    /// 解析落在区间内的整数
    /// </summary>
    /// <remarks>
    /// 按不变文化；不是整数或不在区间内时抛出，<paramref name="name"/> 是报错用的全名。用到它的键都不是机密，原值照写
    /// </remarks>
    private static int ParseInt32(string text, string name, int minimum, int maximum)
        => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum && value <= maximum
            ? value
            : throw new ArgumentException($"'{name}' must be an integer between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}, but was '{text}'.");

    /// <summary>
    /// 解析以秒计、不超过上限的正时长
    /// </summary>
    /// <remarks>
    /// 按不变文化，可带小数；不是有限的正数、不足 100 纳秒或超过 <paramref name="maximum"/> 时抛出，<paramref name="name"/> 是报错用的全名
    /// </remarks>
    private static TimeSpan ParseSeconds(string text, string name, TimeSpan maximum)
    {
        // 先判范围再换算：TimeSpan.FromSeconds 对装不下的数抛 OverflowException，消息里没有键名
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            && double.IsFinite(seconds)
            && seconds > 0
            && seconds <= maximum.TotalSeconds)
        {
            var value = TimeSpan.FromSeconds(seconds);
            // 不足 1 个计时周期（100 纳秒）的正数会被舍成零，零不是合法的超时
            if (value > TimeSpan.Zero)
            {
                return value;
            }
        }

        throw new ArgumentException($"'{name}' must be a positive number of seconds not greater than {maximum.TotalSeconds.ToString(CultureInfo.InvariantCulture)}, but was '{text}'.");
    }

    /// <summary>
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，访问密钥 ID、访问密钥、会话令牌与代理的用户名、口令都不能明文出现（RFC-0014「选项里的凭据」，ADR-0089、ADR-0281）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("ServiceUrl = ").Append(ServiceUrl)
            .Append(", Region = ").Append(Region)
            .Append(", AccessKey = ").Append(Redaction.Redact(AccessKey))
            .Append(", SecretKey = ").Append(Redaction.Redact(SecretKey))
            .Append(", SessionToken = ").Append(Redaction.Redact(SessionToken))
            .Append(", ForcePathStyle = ").Append(ForcePathStyle)
            .Append(", EnsureBuckets = ").Append(EnsureBuckets)
            .Append(", HealthBucket = ").Append(HealthBucket)
            .Append(", ConditionalWrites = ").Append(ConditionalWrites)
            .Append(", HealthProbeInterval = ").Append(HealthProbeInterval)
            .Append(", RequestTimeout = ").Append(RequestTimeout)
            .Append(", MaxAttempts = ").Append(MaxAttempts)
            .Append(", ProxyHost = ").Append(ProxyHost)
            .Append(", ProxyPort = ").Append(ProxyPort)
            .Append(", ProxyUserName = ").Append(Redaction.Redact(ProxyUserName))
            .Append(", ProxyPassword = ").Append(Redaction.Redact(ProxyPassword));
        return true;
    }
}
