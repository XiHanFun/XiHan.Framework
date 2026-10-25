// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.S3;

/// <summary>
/// S3 对象存储选项。从配置节 <c>XiHan:ObjectStorage:S3</c> 显式读取：<c>ServiceUrl</c>、<c>Region</c>、<c>AccessKey</c>、<c>SecretKey</c>、<c>ForcePathStyle</c>、<c>EnsureBuckets</c>；
/// 绑定 <c>objectstorage</c> 存在时其主值与属性（<c>region</c>、<c>accessKey</c>、<c>secretKey</c>、<c>forcePathStyle</c>）覆盖配置。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record S3ObjectStorageOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:ObjectStorage:S3";

    /// <summary>
    /// 服务端点（如 <c>http://localhost:9000</c>、<c>https://oss-cn-hangzhou.aliyuncs.com</c>）；空表示按 <see cref="Region"/> 访问 Amazon S3。
    /// </summary>
    public string? ServiceUrl
    {
        get => field;
        init => field = value is null || Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? value
            : throw new ArgumentException($"The service URL '{value}' must be an absolute http or https URL.", nameof(value));
    }

    /// <summary>
    /// 区域；访问 Amazon S3 时选端点，访问兼容服务时作签名区域。默认 <c>us-east-1</c>。
    /// </summary>
    public string Region
    {
        get => field;
        init => field = !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The region must not be empty.", nameof(value));
    } = "us-east-1";

    /// <summary>
    /// 访问密钥 ID；与 <see cref="SecretKey"/> 都为空时用 AWS SDK 的默认凭据链（环境变量、配置文件、实例角色）。
    /// </summary>
    public string? AccessKey { get; init; }

    /// <summary>
    /// 访问密钥。
    /// </summary>
    public string? SecretKey { get; init; }

    /// <summary>
    /// 桶名放在路径而不是主机名里；兼容服务多数只支持路径式。默认开启。
    /// </summary>
    public bool ForcePathStyle { get; init; } = true;

    /// <summary>
    /// 写入时桶不存在就创建；默认关闭，桶由基础设施预先创建。
    /// </summary>
    public bool EnsureBuckets { get; init; }

    /// <summary>
    /// 从配置读取，再用绑定（若有）覆盖。
    /// </summary>
    public static S3ObjectStorageOptions FromConfiguration(IConfiguration configuration, Maybe<BindingValue> binding = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = new S3ObjectStorageOptions
        {
            ServiceUrl = Text(configuration, "ServiceUrl"),
            AccessKey = Text(configuration, "AccessKey"),
            SecretKey = Text(configuration, "SecretKey"),
        };
        if (Text(configuration, "Region") is { } region)
        {
            options = options with { Region = region };
        }

        if (configuration[$"{ConfigurationSection}:ForcePathStyle"] is { } pathStyle)
        {
            options = options with { ForcePathStyle = ParseBoolean(pathStyle, "ForcePathStyle") };
        }

        if (configuration[$"{ConfigurationSection}:EnsureBuckets"] is { } ensure)
        {
            options = options with { EnsureBuckets = ParseBoolean(ensure, "EnsureBuckets") };
        }

        // 绑定来自部署环境（Service Binding），优先于应用配置
        if (binding.TryGetValue(out var value))
        {
            if (!string.IsNullOrWhiteSpace(value.Value))
            {
                options = options with { ServiceUrl = value.Value };
            }

            if (value.Property("region").TryGetValue(out var boundRegion))
            {
                options = options with { Region = boundRegion };
            }

            if (value.Property("accessKey").TryGetValue(out var accessKey))
            {
                options = options with { AccessKey = accessKey };
            }

            if (value.Property("secretKey").TryGetValue(out var secretKey))
            {
                options = options with { SecretKey = secretKey };
            }

            if (value.Property("forcePathStyle").TryGetValue(out var boundPathStyle))
            {
                options = options with { ForcePathStyle = ParseBoolean(boundPathStyle, "forcePathStyle") };
            }
        }

        return options;
    }

    /// <summary>
    /// 读一个非空文本值；缺失或空白为 <see langword="null"/>。
    /// </summary>
    private static string? Text(IConfiguration configuration, string name)
        => configuration[$"{ConfigurationSection}:{name}"] is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// 解析布尔值；不是 <c>true</c> / <c>false</c> 时抛出。
    /// </summary>
    private static bool ParseBoolean(string text, string name)
        => bool.TryParse(text, out var value) ? value : throw new ArgumentException($"{ConfigurationSection}:{name} must be 'true' or 'false', but was '{text}'.");
}
