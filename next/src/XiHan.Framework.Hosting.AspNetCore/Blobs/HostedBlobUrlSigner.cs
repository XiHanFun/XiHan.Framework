// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using XiHan.Framework.ObjectStorage.Abstractions;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Signing;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Hosting.AspNetCore.Blobs;

/// <summary>
/// 宿主代收发的预签名地址：给没有原生签名的存储（文件系统）签出指向 <c>MapXiHanBlobs()</c> 端点的地址，端点验签后经 <c>IBlobStore</c> / <c>IBlobMultipartStore</c> 收发内容。
/// 地址为 <c>&lt;BaseUrl&gt;/&lt;container&gt;/&lt;key&gt;?x-method=…&amp;x-expires=…[&amp;x-content-type=…][&amp;x-filename=…][&amp;x-upload=…&amp;x-part=…]&amp;x-signature=…</c>，
/// 签名是对方法、容器、键、过期时刻（Unix 秒）与各限定项的 HMAC-SHA256（Base64Url）；无状态，同一密钥的多个实例互认，密钥轮换即令旧地址失效。
/// </summary>
/// <param name="options">选项。</param>
/// <param name="clock">时钟；过期时刻按它计算与判定。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class HostedBlobUrlSigner(HostedBlobUrlOptions options, TimeProvider clock) : IBlobUrlSigner
{
    /// <summary>
    /// 方法参数。
    /// </summary>
    internal const string MethodParameter = "x-method";

    /// <summary>
    /// 过期时刻参数（Unix 秒）。
    /// </summary>
    internal const string ExpiresParameter = "x-expires";

    /// <summary>
    /// 上传限定的内容类型参数。
    /// </summary>
    internal const string ContentTypeParameter = "x-content-type";

    /// <summary>
    /// 下载保存的文件名参数。
    /// </summary>
    internal const string FileNameParameter = "x-filename";

    /// <summary>
    /// 分片上传标识参数。
    /// </summary>
    internal const string UploadParameter = "x-upload";

    /// <summary>
    /// 分片编号参数。
    /// </summary>
    internal const string PartParameter = "x-part";

    /// <summary>
    /// 签名参数。
    /// </summary>
    internal const string SignatureParameter = "x-signature";

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly HostedBlobUrlOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    /// <summary>
    /// 时钟。
    /// </summary>
    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    /// <summary>
    /// 选项。
    /// </summary>
    public HostedBlobUrlOptions Options => _options;

    /// <inheritdoc />
    public ValueTask<Result<BlobSignedUrl>> SignAsync(ContainerName container, BlobKey key, BlobUrlRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Upload is { } upload && (upload.Container != container || upload.Key != key))
        {
            throw new ArgumentException("The multipart upload belongs to another blob.", nameof(request));
        }

        // 过期时刻按秒截断后再签，地址里的值与签名、返回的时刻三者一致
        var expires = (_clock.GetUtcNow() + request.ExpiresIn).ToUnixTimeSeconds();
        var method = MethodName(request.Method);
        var part = request.PartNumber?.ToString(CultureInfo.InvariantCulture);
        var query = new List<(string Name, string Value)> { (MethodParameter, method), (ExpiresParameter, expires.ToString(CultureInfo.InvariantCulture)) };
        if (request.ContentType is { } contentType)
        {
            query.Add((ContentTypeParameter, contentType));
        }

        if (request.DownloadFileName is { } fileName)
        {
            query.Add((FileNameParameter, fileName));
        }

        if (request.Upload is { } multipart)
        {
            query.Add((UploadParameter, multipart.UploadId));
            query.Add((PartParameter, part!));
        }

        query.Add((SignatureParameter, Sign(method, container.Value, key.Value, expires, request.ContentType, request.DownloadFileName, request.Upload?.UploadId, part)));
        var path = string.Join('/', key.Value.Split('/').Select(Uri.EscapeDataString));
        var url = $"{_options.BaseUrl}/{container.Value}/{path}?{string.Join('&', query.Select(static q => q.Name + "=" + Uri.EscapeDataString(q.Value)))}";
        var headers = request.ContentType is { } required
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = required }
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        return ValueTask.FromResult(Result.Success(new BlobSignedUrl(new Uri(url, UriKind.RelativeOrAbsolute), request.Method, DateTimeOffset.FromUnixTimeSeconds(expires), headers)));
    }

    /// <summary>
    /// 验签：方法相符、参数齐全、签名一致（定时比较），再看是否过期；签名不符先于过期判定，篡改过期时刻即签名不符。
    /// </summary>
    internal Result<VerifiedBlobUrl> Verify(BlobUrlMethod method, ContainerName container, BlobKey key, IQueryCollection query)
    {
        var expected = MethodName(method);
        string? Single(string name) => query.TryGetValue(name, out var values) && values.Count == 1 ? values[0] : null;
        var expiresText = Single(ExpiresParameter);
        var signature = Single(SignatureParameter);
        if (!string.Equals(Single(MethodParameter), expected, StringComparison.Ordinal)
            || signature is null
            || !long.TryParse(expiresText, NumberStyles.None, CultureInfo.InvariantCulture, out var expires))
        {
            return Error.Denied(ObjectStorageErrors.SignatureInvalid, ObjectStorageMessages.SignedUrlInvalid);
        }

        var contentType = Single(ContentTypeParameter);
        var fileName = Single(FileNameParameter);
        var upload = Single(UploadParameter);
        var partText = Single(PartParameter);
        int? part = null;
        if ((upload is null) != (partText is null))
        {
            return Error.Denied(ObjectStorageErrors.SignatureInvalid, ObjectStorageMessages.SignedUrlInvalid);
        }

        if (partText is not null)
        {
            if (!int.TryParse(partText, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                return Error.Denied(ObjectStorageErrors.SignatureInvalid, ObjectStorageMessages.SignedUrlInvalid);
            }

            part = number;
        }

        var computed = Encoding.ASCII.GetBytes(Sign(expected, container.Value, key.Value, expires, contentType, fileName, upload, partText));
        if (!CryptographicOperations.FixedTimeEquals(computed, Encoding.ASCII.GetBytes(signature)))
        {
            return Error.Denied(ObjectStorageErrors.SignatureInvalid, ObjectStorageMessages.SignedUrlInvalid);
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expires);
        if (_clock.GetUtcNow() > expiresAt)
        {
            return Error.Denied(ObjectStorageErrors.SignatureInvalid, ObjectStorageMessages.SignedUrlExpired, MessageArguments.Of(("expiresAt", expiresAt)));
        }

        return Result.Success(new VerifiedBlobUrl(contentType, fileName, upload, part));
    }

    /// <summary>
    /// HMAC-SHA256（Base64Url，无填充）：规范串按行连接版本、方法、容器、键、过期时刻与各限定项（缺省为空行）。
    /// </summary>
    private string Sign(string method, string container, string key, long expires, string? contentType, string? fileName, string? upload, string? part)
    {
        var canonical = string.Join('\n', "xihan-blob-v1", method, container, key, expires.ToString(CultureInfo.InvariantCulture), contentType ?? string.Empty, fileName ?? string.Empty, upload ?? string.Empty, part ?? string.Empty);
        var mac = HMACSHA256.HashData(_options.Secret.Span, Encoding.UTF8.GetBytes(canonical));
        return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>
    /// 方法名。
    /// </summary>
    private static string MethodName(BlobUrlMethod method) => method == BlobUrlMethod.Get ? "GET" : "PUT";
}
