// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.DataSources;

/// <summary>
/// 建数据源失败时的标准错误
/// </summary>
/// <remarks>
/// <see cref="IDataSourceProvider.CreateAsync"/> 以结果报告失败（ADR-0150）；各 Provider 用这里的工厂，原因在这里统一脱敏
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class DataSourceFailures
{
    /// <summary>
    /// 定义不合法（数据库类型不认得、连接串格式不对）
    /// </summary>
    /// <param name="definition">数据源定义</param>
    /// <param name="reason">原因，通常是驱动的解析消息；经 <see cref="DataSourceDefinition.Redact"/> 去掉连接串</param>
    /// <returns><see cref="PersistenceErrors.DataSourceInvalid"/>，参数 <c>name</c>、<c>reason</c></returns>
    public static Error Invalid(DataSourceDefinition definition, string reason)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(reason);
        return Error.Validation(PersistenceErrors.DataSourceInvalid, PersistenceMessages.DataSourceInvalid, MessageArguments.Of(("name", definition.Name), ("reason", definition.Redact(reason))));
    }
}
