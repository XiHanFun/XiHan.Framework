// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Hosting.AspNetCore.Blobs;

/// <summary>
/// 验过签名、未过期的地址所授的权：上传限定的内容类型、下载保存的文件名、分片上传的标识与编号。
/// </summary>
/// <param name="ContentType">上传限定的内容类型。</param>
/// <param name="DownloadFileName">下载保存的文件名。</param>
/// <param name="UploadId">分片上传的标识。</param>
/// <param name="PartNumber">分片编号。</param>
internal sealed record VerifiedBlobUrl(string? ContentType, string? DownloadFileName, string? UploadId, int? PartNumber);
