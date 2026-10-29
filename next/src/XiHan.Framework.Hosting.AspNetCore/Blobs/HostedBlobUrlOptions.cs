// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Blobs;

/// <summary>
/// 宿主代收发的预签名地址选项。从配置节 <c>XiHan:ObjectStorage:SignedUrls</c> 显式读取：<c>BaseUrl</c>（缺省 <c>/blobs</c>）、<c>Secret</c>（必填，UTF-8 至少 32 字节；多实例部署须一致）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record HostedBlobUrlOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:ObjectStorage:SignedUrls";

    /// <summary>
    /// 签名密钥的最小字节数。
    /// </summary>
    public const int MinSecretBytes = 32;

    /// <summary>
    /// 地址前缀：绝对地址（<c>https://files.example.com/blobs</c>）或以 <c>/</c> 开头的路径（相对应用的源，缺省 <c>/blobs</c>）；末尾的 <c>/</c> 去掉。
    /// </summary>
    public string BaseUrl
    {
        get => field;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            var trimmed = value.TrimEnd('/');
            var valid = trimmed.StartsWith('/') || (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute) && absolute.Scheme is "http" or "https" && string.IsNullOrEmpty(absolute.Query));
            field = valid && trimmed.Length > 0 ? trimmed : throw new ArgumentException($"The signed URL base '{value}' must be an absolute http(s) URL without a query or a path starting with '/'.", nameof(value));
        }
    } = "/blobs";

    /// <summary>
    /// HMAC-SHA256 签名密钥（至少 <see cref="MinSecretBytes"/> 字节）。
    /// </summary>
    public required ReadOnlyMemory<byte> Secret
    {
        get => field;
        init => field = value.Length >= MinSecretBytes ? value : throw new ArgumentException($"The signing secret must be at least {MinSecretBytes} bytes.", nameof(value));
    }

    /// <summary>
    /// 端点的路由前缀：<see cref="BaseUrl"/> 的路径部分。
    /// </summary>
    public string BasePath => BaseUrl.StartsWith('/') ? BaseUrl : new Uri(BaseUrl).AbsolutePath.TrimEnd('/');

    /// <summary>
    /// 从配置读取；<c>Secret</c> 缺失或太短时抛出。
    /// </summary>
    public static HostedBlobUrlOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var secret = configuration[$"{ConfigurationSection}:Secret"];
        if (string.IsNullOrEmpty(secret) || Encoding.UTF8.GetByteCount(secret) < MinSecretBytes)
        {
            throw new ArgumentException($"{ConfigurationSection}:Secret is required and must be at least {MinSecretBytes} bytes of UTF-8 text.");
        }

        var options = new HostedBlobUrlOptions { Secret = Encoding.UTF8.GetBytes(secret) };
        return configuration[$"{ConfigurationSection}:BaseUrl"] is { } baseUrl && !string.IsNullOrWhiteSpace(baseUrl) ? options with { BaseUrl = baseUrl } : options;
    }
}
