// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// S3 对象存储选项。
/// </summary>
/// <remarks>
/// 从配置节 <c>XiHan:ObjectStorage:S3</c> 显式读取：<c>ServiceUrl</c>、<c>Region</c>、<c>AccessKey</c>、<c>SecretKey</c>、<c>ForcePathStyle</c>、<c>EnsureBuckets</c>、<c>HealthBucket</c>、<c>HealthProbeIntervalSeconds</c>；绑定 <c>objectstorage</c> 存在时其主值与属性（<c>region</c>、<c>accessKey</c>、<c>secretKey</c>、<c>forcePathStyle</c>）逐项覆盖配置，空白的不算给出（RFC-0016「资源绑定与配置节」）。命名实例（Provider <c>s3</c>）的设置用同样的键（不含 <c>HealthProbeIntervalSeconds</c>）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record S3ObjectStorageOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:ObjectStorage:S3";

    /// <summary>
    /// 服务端点（如 <c>http://localhost:9000</c>、<c>https://oss-cn-hangzhou.aliyuncs.com</c>）。
    /// </summary>
    /// <remarks>
    /// 空表示按 <see cref="Region"/> 访问 Amazon S3。
    /// </remarks>
    public string? ServiceUrl
    {
        get => field;
        init => field = value is null || Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? value
            : throw new ArgumentException($"The service URL '{value}' must be an absolute http or https URL.", nameof(value));
    }

    /// <summary>
    /// 区域。
    /// </summary>
    /// <remarks>
    /// 访问 Amazon S3 时选端点，访问兼容服务时作签名区域。默认 <c>us-east-1</c>。
    /// </remarks>
    public string Region
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The region must not be empty.", nameof(value));
    } = "us-east-1";

    /// <summary>
    /// 访问密钥 ID。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="SecretKey"/> 都为空时用 AWS SDK 的默认凭据链（环境变量、配置文件、实例角色）。
    /// </remarks>
    public string? AccessKey { get; init; }

    /// <summary>
    /// 访问密钥。
    /// </summary>
    public string? SecretKey { get; init; }

    /// <summary>
    /// 桶名放在路径而不是主机名里。
    /// </summary>
    /// <remarks>
    /// 兼容服务多数只支持路径式。默认开启。
    /// </remarks>
    public bool ForcePathStyle { get; init; } = true;

    /// <summary>
    /// 写入时桶不存在就创建。
    /// </summary>
    /// <remarks>
    /// 默认关闭，桶由基础设施预先创建。
    /// </remarks>
    public bool EnsureBuckets { get; init; }

    /// <summary>
    /// 健康项 <c>objectstorage.s3.connection</c> 探测的桶。
    /// </summary>
    /// <remarks>
    /// 有值时对它 HEAD（同时验证凭据对该桶的权限）；为 <see langword="null"/> 时列桶。
    /// </remarks>
    public string? HealthBucket { get; init; }

    /// <summary>
    /// 健康项 <c>objectstorage.s3.connection</c> 的重探间隔（也是单次探测的超时）。
    /// </summary>
    /// <remarks>
    /// 默认 15 秒。
    /// </remarks>
    public TimeSpan HealthProbeInterval
    {
        get => field;
        init => field = value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The health probe interval must be positive.");
    } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// 命名实例的设置键。
    /// </summary>
    /// <remarks>
    /// 探测间隔由注册表统一（<c>XiHan:ObjectStorage:StoreHealthProbeIntervalSeconds</c>），不在其中。
    /// </remarks>
    private static readonly string[] _settingKeys = ["ServiceUrl", "Region", "AccessKey", "SecretKey", "ForcePathStyle", "EnsureBuckets", "HealthBucket"];

    /// <summary>
    /// 从配置读取，再用绑定（若有）覆盖。
    /// </summary>
    public static S3ObjectStorageOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = Parse(name => configuration[$"{ConfigurationSection}:{name}"], ConfigurationSection + ":");
        if (configuration[$"{ConfigurationSection}:HealthProbeIntervalSeconds"] is { } probe)
        {
            options = double.TryParse(probe, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? options with { HealthProbeInterval = TimeSpan.FromSeconds(seconds) }
                : throw new ArgumentException($"{ConfigurationSection}:HealthProbeIntervalSeconds must be a positive number, but was '{probe}'.");
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

            if (BoundText(value, "forcePathStyle") is { } boundPathStyle)
            {
                options = options with { ForcePathStyle = ParseBoolean(boundPathStyle, "The binding property forcePathStyle") };
            }
        }

        return options;
    }

    /// <summary>
    /// 读绑定的一个非空属性。
    /// </summary>
    /// <remarks>
    /// 没有该属性或属性空白为 <see langword="null"/>。
    /// </remarks>
    private static string? BoundText(BindingValue binding, string key)
        => binding.Property(key).TryGetValue(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    /// <summary>
    /// 从命名实例的设置读取。
    /// </summary>
    /// <remarks>
    /// 键同配置节，不区分大小写；没有绑定覆盖。未知的键抛出。
    /// </remarks>
    public static S3ObjectStorageOptions FromSettings(IReadOnlyDictionary<string, string> settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var lookup = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
        if (lookup.Keys.FirstOrDefault(static key => !_settingKeys.Contains(key, StringComparer.OrdinalIgnoreCase)) is { } unknown)
        {
            throw new ArgumentException($"Unknown S3 setting '{unknown}'; the known settings are {string.Join(", ", _settingKeys)}.", nameof(settings));
        }

        return Parse(name => lookup.TryGetValue(name, out var value) ? value : null, string.Empty);
    }

    /// <summary>
    /// 按名读取连接与行为各项（不含探测间隔）。
    /// </summary>
    /// <remarks>
    /// <paramref name="prefix"/> 只用于报错。
    /// </remarks>
    private static S3ObjectStorageOptions Parse(Func<string, string?> read, string prefix)
    {
        var options = new S3ObjectStorageOptions
        {
            ServiceUrl = Text(read, "ServiceUrl"),
            AccessKey = Text(read, "AccessKey"),
            SecretKey = Text(read, "SecretKey"),
            HealthBucket = Text(read, "HealthBucket"),
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

        return options;
    }

    /// <summary>
    /// 读一个非空文本值。
    /// </summary>
    /// <remarks>
    /// 缺失或空白为 <see langword="null"/>。
    /// </remarks>
    private static string? Text(Func<string, string?> read, string name)
        => read(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// 解析布尔值。
    /// </summary>
    /// <remarks>
    /// 不是 <c>true</c> / <c>false</c> 时抛出，<paramref name="name"/> 是报错用的全名。
    /// </remarks>
    private static bool ParseBoolean(string text, string name)
        => bool.TryParse(text, out var value) ? value : throw new ArgumentException($"{name} must be 'true' or 'false', but was '{text}'.");

    /// <summary>
    /// 打印成员，凭据以 <see cref="Redaction.Mask"/> 代替。
    /// </summary>
    /// <remarks>
    /// 记录的 <c>ToString()</c> 经它输出：对象可能整个进日志与异常消息，访问密钥（访问密钥 ID 随请求明文发送，照常打印）不能明文出现（RFC-0014「选项里的凭据」）。其余成员同编译器生成的写法；新加属性时在这里一并补上，架构测试 <c>CredentialRedactionTests</c> 会核对。
    /// </remarks>
    private bool PrintMembers(StringBuilder builder)
    {
        // 凭据只打印遮蔽文本，null 仍打印为空：看得出有没有配置，看不到值
        builder.Append("ServiceUrl = ").Append(ServiceUrl)
            .Append(", Region = ").Append(Region)
            .Append(", AccessKey = ").Append(AccessKey)
            .Append(", SecretKey = ").Append(Redaction.Redact(SecretKey))
            .Append(", ForcePathStyle = ").Append(ForcePathStyle)
            .Append(", EnsureBuckets = ").Append(EnsureBuckets)
            .Append(", HealthBucket = ").Append(HealthBucket)
            .Append(", HealthProbeInterval = ").Append(HealthProbeInterval);
        return true;
    }
}
