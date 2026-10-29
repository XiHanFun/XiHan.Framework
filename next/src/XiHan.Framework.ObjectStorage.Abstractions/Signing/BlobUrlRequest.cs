// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Signing;

/// <summary>
/// 要签的地址。
/// </summary>
/// <remarks>
/// 方法、有效期（缺省 <see cref="DefaultExpiresIn"/>，与 4.x 一致），下载可带保存的文件名，上传可限定内容类型，或者是某次分片上传的某一片。用 <see cref="ForDownload"/>、<see cref="ForUpload"/>、<see cref="ForPart"/> 构造。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record BlobUrlRequest
{
    /// <summary>
    /// 缺省有效期：30 分钟。
    /// </summary>
    public static readonly TimeSpan DefaultExpiresIn = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 最长有效期：7 天（S3 签名 V4 的上限）。
    /// </summary>
    public static readonly TimeSpan MaxExpiresIn = TimeSpan.FromDays(7);

    /// <summary>
    /// 创建。
    /// </summary>
    private BlobUrlRequest(BlobUrlMethod method, TimeSpan? expiresIn, string? contentType, string? downloadFileName, BlobMultipartUpload? upload, int? partNumber)
    {
        var lifetime = expiresIn ?? DefaultExpiresIn;
        if (lifetime < TimeSpan.FromSeconds(1) || lifetime > MaxExpiresIn)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresIn), lifetime, "The signed URL lifetime must be between 1 second and 7 days.");
        }

        Method = method;
        ExpiresIn = lifetime;
        ContentType = contentType;
        DownloadFileName = downloadFileName;
        Upload = upload;
        PartNumber = partNumber;
    }

    /// <summary>
    /// 方法。
    /// </summary>
    public BlobUrlMethod Method { get; }

    /// <summary>
    /// 有效期。
    /// </summary>
    public TimeSpan ExpiresIn { get; }

    /// <summary>
    /// 上传时客户端必须发送的内容类型（也成为对象的内容类型）。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时不限定。
    /// </remarks>
    public string? ContentType { get; }

    /// <summary>
    /// 下载时浏览器保存的文件名（响应带 <c>Content-Disposition: attachment</c>）。
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时不带。
    /// </remarks>
    public string? DownloadFileName { get; }

    /// <summary>
    /// 分片上传。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="PartNumber"/> 一起给出时地址只能上传这一片。
    /// </remarks>
    public BlobMultipartUpload? Upload { get; }

    /// <summary>
    /// 分片编号。
    /// </summary>
    public int? PartNumber { get; }

    /// <summary>
    /// 下载地址。
    /// </summary>
    /// <param name="expiresIn">有效期；缺省 30 分钟。</param>
    /// <param name="downloadFileName">保存的文件名；缺省不带 <c>Content-Disposition</c>。</param>
    public static BlobUrlRequest ForDownload(TimeSpan? expiresIn = null, string? downloadFileName = null)
        => new(BlobUrlMethod.Get, expiresIn, null, NonEmptyOrNull(downloadFileName, nameof(downloadFileName)), null, null);

    /// <summary>
    /// 整个对象的上传地址（覆盖已有对象）。
    /// </summary>
    /// <param name="expiresIn">有效期；缺省 30 分钟。</param>
    /// <param name="contentType">客户端必须发送的内容类型；缺省不限定。</param>
    public static BlobUrlRequest ForUpload(TimeSpan? expiresIn = null, string? contentType = null)
        => new(BlobUrlMethod.Put, expiresIn, NonEmptyOrNull(contentType, nameof(contentType)), null, null, null);

    /// <summary>
    /// 分片上传中某一片的上传地址。
    /// </summary>
    /// <remarks>
    /// 响应的 <c>ETag</c> 头即该分片的 ETag。
    /// </remarks>
    /// <param name="upload">分片上传。</param>
    /// <param name="partNumber">编号。</param>
    /// <param name="expiresIn">有效期；缺省 30 分钟。</param>
    public static BlobUrlRequest ForPart(BlobMultipartUpload upload, int partNumber, TimeSpan? expiresIn = null)
    {
        ArgumentNullException.ThrowIfNull(upload);
        return new(BlobUrlMethod.Put, expiresIn, null, null, upload, BlobMultipart.ValidatePartNumber(partNumber));
    }

    /// <summary>
    /// 可选文本：给了就不能是空白。
    /// </summary>
    private static string? NonEmptyOrNull(string? value, string name)
        => value is null || !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("The value must not be blank.", name);
}
