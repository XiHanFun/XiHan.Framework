// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions;

/// <summary>
/// 对象存储的错误码。
/// </summary>
/// <remarks>
/// 登记在 <c>spec/conventions/error-codes.md</c>。标识不合法是编程错误，在构造时抛出，不占错误码。
/// </remarks>
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

    /// <summary>
    /// 命名存储实例不存在（没有运行时登记，配置里也没有定义）。
    /// </summary>
    public const string StoreNotFound = "OBJECTSTORAGE_NOTFOUND_003";

    /// <summary>
    /// 命名存储实例的定义不合法（Provider 未登记或设置不合法）。
    /// </summary>
    public const string StoreDefinitionInvalid = "OBJECTSTORAGE_VALIDATION_003";

    /// <summary>
    /// 缺省存储的能力选择不一致。
    /// </summary>
    /// <remarks>
    /// 存储包被选为分片上传或预签名，却没有被选为对象存储。装配错误，以异常抛出，应用启动失败。
    /// </remarks>
    public const string StoreSelectionInvalid = "OBJECTSTORAGE_VALIDATION_004";

    /// <summary>
    /// 列举的页大小越界。
    /// </summary>
    /// <remarks>
    /// <see cref="Listing.BlobQuery.PageSize"/> 不在 1 到 <see cref="Listing.BlobQuery.MaxPageSize"/> 之间；各 Provider 列举时以结果返回（HTTP 400），不访问存储。
    /// </remarks>
    public const string PageSizeInvalid = "OBJECTSTORAGE_VALIDATION_005";

    /// <summary>
    /// 不覆盖写入在本存储上无法保证。
    /// </summary>
    /// <remarks>
    /// S3 兼容服务不认条件写入（<c>If-None-Match: *</c>），启动或建实例时的探测发现后失败；或存储以 <c>ConditionalWrites = false</c> 关闭了条件写入，<c>Overwrite = false</c> 的写入与分片上传一律以它失败，不访问存储（ADR-0089）。
    /// </remarks>
    public const string ConditionalWriteUnavailable = "OBJECTSTORAGE_UNAVAILABLE_003";

    /// <summary>
    /// 存储拒绝了探测请求。
    /// </summary>
    /// <remarks>
    /// 条件写入探测写探测键、建桶或中止探测用的分片上传时，凭据无效或权限不足（HTTP 403）；启动或建实例因此失败（ADR-0089）。
    /// </remarks>
    public const string ProbeDenied = "OBJECTSTORAGE_DENIED_002";
}
