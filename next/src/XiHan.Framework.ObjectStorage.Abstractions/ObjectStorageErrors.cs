// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions;

/// <summary>
/// 对象存储的错误码；登记在 <c>spec/conventions/error-codes.md</c>。标识不合法是编程错误，在构造时抛出，不占错误码。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class ObjectStorageErrors
{
    /// <summary>
    /// 对象不存在。
    /// </summary>
    public const string BlobNotFound = "OBJECTSTORAGE_NOTFOUND_001";

    /// <summary>
    /// 对象已存在且不允许覆盖。
    /// </summary>
    public const string BlobExists = "OBJECTSTORAGE_CONFLICT_001";

    /// <summary>
    /// 存储不可达或 IO 失败。
    /// </summary>
    public const string StoreUnavailable = "OBJECTSTORAGE_UNAVAILABLE_001";

    /// <summary>
    /// 配额或单对象大小超限。
    /// </summary>
    public const string QuotaExceeded = "OBJECTSTORAGE_UNAVAILABLE_002";

    /// <summary>
    /// 读取范围的起点不在对象内。
    /// </summary>
    public const string RangeNotSatisfiable = "OBJECTSTORAGE_VALIDATION_001";

    /// <summary>
    /// 完成分片上传时的分片清单不合法（为空、乱序、未上传或 ETag 不符、非末片过小）。
    /// </summary>
    public const string PartsInvalid = "OBJECTSTORAGE_VALIDATION_002";

    /// <summary>
    /// 分片上传不存在或已结束。
    /// </summary>
    public const string UploadNotFound = "OBJECTSTORAGE_NOTFOUND_002";

    /// <summary>
    /// 预签名地址无效或已过期。
    /// </summary>
    public const string SignatureInvalid = "OBJECTSTORAGE_DENIED_001";
}
