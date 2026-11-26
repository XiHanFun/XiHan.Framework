// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.ObjectStorage.Abstractions.Blobs;
using XiHan.Framework.ObjectStorage.Abstractions.Keys;
using XiHan.Framework.ObjectStorage.Abstractions.Multipart;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Signing;

/// <summary>
/// 要签的地址
/// </summary>
/// <remarks>
/// 方法、有效期（缺省 <see cref="DefaultExpiresIn"/>），下载可带保存的文件名，上传可限定内容类型，或者是某次分片上传的某一片。用 <see cref="ForDownload()"/>、<see cref="ForUpload()"/>、<see cref="ForPart(BlobMultipartUpload, int)"/> 构造
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public sealed record BlobUrlRequest
{
    /// <summary>
    /// 缺省有效期：30 分钟
    /// </summary>
    public static readonly TimeSpan DefaultExpiresIn = TimeSpan.FromMinutes(30);

    /// <summary>
    /// 最长有效期：7 天（S3 签名 V4 的上限）
    /// </summary>
    public static readonly TimeSpan MaxExpiresIn = TimeSpan.FromDays(7);

    /// <summary>
    /// 保存的文件名 UTF-8 编码后的最大字节数
    /// </summary>
    /// <remarks>
    /// 不是 <see cref="string.Length"/>：含非 ASCII 字符的文件名能写的字符数少于这个数（RFC-0012，ADR-0282）
    /// </remarks>
    public const int MaxDownloadFileNameBytes = 255;

    /// <summary>
    /// 创建
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
    /// 方法
    /// </summary>
    public BlobUrlMethod Method { get; }

    /// <summary>
    /// 有效期
    /// </summary>
    public TimeSpan ExpiresIn { get; }

    /// <summary>
    /// 上传时客户端必须发送的内容类型（也成为对象的内容类型）
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时不限定
    /// </remarks>
    public string? ContentType { get; }

    /// <summary>
    /// 下载时浏览器保存的文件名（响应带 <c>Content-Disposition: attachment</c>）
    /// </summary>
    /// <remarks>
    /// 为 <see langword="null"/> 时不带
    /// </remarks>
    public string? DownloadFileName { get; }

    /// <summary>
    /// 分片上传
    /// </summary>
    /// <remarks>
    /// 与 <see cref="PartNumber"/> 一起给出时地址只能上传这一片
    /// </remarks>
    public BlobMultipartUpload? Upload { get; }

    /// <summary>
    /// 分片编号
    /// </summary>
    public int? PartNumber { get; }

    /// <summary>
    /// 下载地址
    /// </summary>
    /// <remarks>
    /// <c>expiresIn</c> 取 <c>null</c>、<c>downloadFileName</c> 取 <c>null</c>
    /// </remarks>
    public static BlobUrlRequest ForDownload()
        => ForDownload(null, null);

    /// <summary>
    /// 下载地址
    /// </summary>
    /// <remarks>
    /// <c>downloadFileName</c> 取 <c>null</c>
    /// </remarks>
    /// <param name="expiresIn">有效期；缺省 30 分钟</param>
    public static BlobUrlRequest ForDownload(TimeSpan? expiresIn)
        => ForDownload(expiresIn, null);

    /// <summary>
    /// 下载地址
    /// </summary>
    /// <param name="expiresIn">有效期；缺省 30 分钟</param>
    /// <param name="downloadFileName">保存的文件名，不全是空白、不含控制字符与孤立的代理项、UTF-8 编码后不超过 <see cref="MaxDownloadFileNameBytes"/> 字节（ADR-0219、ADR-0282、ADR-0316）；缺省不带 <c>Content-Disposition</c></param>
    public static BlobUrlRequest ForDownload(TimeSpan? expiresIn, string? downloadFileName)
        => new(BlobUrlMethod.Get, expiresIn, null, FileNameOrNull(downloadFileName), null, null);

    /// <summary>
    /// 整个对象的上传地址（覆盖已有对象）
    /// </summary>
    /// <remarks>
    /// <c>expiresIn</c> 取 <c>null</c>、<c>contentType</c> 取 <c>null</c>
    /// </remarks>
    public static BlobUrlRequest ForUpload()
        => ForUpload(null, null);

    /// <summary>
    /// 整个对象的上传地址（覆盖已有对象）
    /// </summary>
    /// <remarks>
    /// <c>contentType</c> 取 <c>null</c>
    /// </remarks>
    /// <param name="expiresIn">有效期；缺省 30 分钟</param>
    public static BlobUrlRequest ForUpload(TimeSpan? expiresIn)
        => ForUpload(expiresIn, null);

    /// <summary>
    /// 整个对象的上传地址（覆盖已有对象）
    /// </summary>
    /// <param name="expiresIn">有效期；缺省 30 分钟</param>
    /// <param name="contentType">客户端必须发送的内容类型，须满足 <see cref="BlobPutOptions.IsValidContentType"/>（ADR-0219）；缺省不限定</param>
    public static BlobUrlRequest ForUpload(TimeSpan? expiresIn, string? contentType)
        => new(BlobUrlMethod.Put, expiresIn, ContentTypeOrNull(contentType), null, null, null);

    /// <summary>
    /// 分片上传中某一片的上传地址
    /// </summary>
    /// <remarks>
    /// 响应的 <c>ETag</c> 头即该分片的 ETag
    /// <c>expiresIn</c> 取 <c>null</c>
    /// </remarks>
    /// <param name="upload">分片上传</param>
    /// <param name="partNumber">编号</param>
    public static BlobUrlRequest ForPart(BlobMultipartUpload upload, int partNumber)
        => ForPart(upload, partNumber, null);

    /// <summary>
    /// 分片上传中某一片的上传地址
    /// </summary>
    /// <remarks>
    /// 响应的 <c>ETag</c> 头即该分片的 ETag
    /// </remarks>
    /// <param name="upload">分片上传</param>
    /// <param name="partNumber">编号</param>
    /// <param name="expiresIn">有效期；缺省 30 分钟</param>
    public static BlobUrlRequest ForPart(BlobMultipartUpload upload, int partNumber, TimeSpan? expiresIn)
    {
        ArgumentNullException.ThrowIfNull(upload);
        return new(BlobUrlMethod.Put, expiresIn, null, null, upload, BlobMultipart.ValidatePartNumber(partNumber));
    }

    /// <summary>
    /// 可选的内容类型：给了就要合内容类型文法
    /// </summary>
    /// <remarks>
    /// 宿主签名的规范串以换行分隔各限定项，值里带换行就能把一项挪成另一项；S3 的响应头覆盖也会把它写进头（ADR-0219）
    /// </remarks>
    private static string? ContentTypeOrNull(string? contentType)
        => contentType is null || BlobPutOptions.IsValidContentType(contentType)
            ? contentType
            : throw new ArgumentException($"The content type must be a media type 'type/subtype', optionally followed by ';' parameters of printable ASCII, not ending with a space and at most {BlobPutOptions.MaxContentTypeLength} characters long.", nameof(contentType));

    /// <summary>
    /// 可选的保存文件名：给了就不能是空白、不能含控制字符与孤立的代理项、不能过长
    /// </summary>
    /// <remarks>
    /// 它进签名的规范串与 <c>Content-Disposition</c> 头，换行同样能挪动限定项或拆出别的头（ADR-0219）。长度按 UTF-8 字节计（ADR-0282）。孤立的代理项编不成 UTF-8，宽松编码会把它签成另一个文件名（ADR-0316）
    /// </remarks>
    private static string? FileNameOrNull(string? fileName)
    {
        if (fileName is null)
        {
            return null;
        }

        // 每个码元至少编成一个字节：码元数已经超过上限的不必再数字节
        return !string.IsNullOrWhiteSpace(fileName) && fileName.Length <= MaxDownloadFileNameBytes && !fileName.Any(char.IsControl) && !BlobKey.HasLoneSurrogate(fileName) && Encoding.UTF8.GetByteCount(fileName) <= MaxDownloadFileNameBytes
            ? fileName
            : throw new ArgumentException($"The download file name must not be blank, must be at most {MaxDownloadFileNameBytes} bytes long in UTF-8 and must not contain control characters or lone surrogates.", nameof(fileName));
    }
}
