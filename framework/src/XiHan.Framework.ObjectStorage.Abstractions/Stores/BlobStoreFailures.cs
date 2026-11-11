// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.ObjectStorage.Abstractions.Stores;

/// <summary>
/// 建存储实例失败时的标准错误
/// </summary>
/// <remarks>
/// <see cref="IBlobStoreProvider.CreateAsync"/> 以结果报告失败（ADR-0150）；各 Provider 用这里的工厂，让注册表与调用方看到同一组错误码与参数
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class BlobStoreFailures
{
    /// <summary>
    /// 定义的设置不合法（含未知的设置键）
    /// </summary>
    /// <param name="definition">存储定义</param>
    /// <param name="reason">原因，通常是设置校验的异常消息</param>
    /// <returns><see cref="ObjectStorageErrors.StoreDefinitionInvalid"/>，参数 <c>store</c>、<c>reason</c></returns>
    public static Error DefinitionInvalid(BlobStoreDefinition definition, string reason)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(reason);
        return Error.Validation(ObjectStorageErrors.StoreDefinitionInvalid, ObjectStorageMessages.StoreDefinitionInvalid, MessageArguments.Of(("store", definition.Name), ("reason", reason)));
    }

    /// <summary>
    /// 后端此刻建不出实例（目录无权限、磁盘错误）
    /// </summary>
    /// <remarks>
    /// 不是定义的问题，下次取用会重试。IO 异常的消息常带完整路径，<c>reason</c> 只写异常类型，完整异常由调用方记进日志
    /// </remarks>
    /// <param name="exception">后端抛出的异常</param>
    /// <returns><see cref="ObjectStorageErrors.StoreUnavailable"/>，参数 <c>reason</c></returns>
    public static Error Unavailable(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var type = exception.GetType();
        return Error.Unavailable(ObjectStorageErrors.StoreUnavailable, ObjectStorageMessages.StoreUnavailable, MessageArguments.Of(("reason", type.FullName ?? type.Name)));
    }
}
