// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.Schema;

/// <summary>
/// 库结构读取的登记
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class SchemaServiceCollectionExtensions
{
    /// <summary>
    /// 登记一个总是返回不支持的 <see cref="ISchemaReader"/>（已有登记时不动）
    /// </summary>
    /// <remarks>
    /// 不能读库结构的持久化 Provider（EF Core、内存实现）调它：取读取器的代码得到明确的 <see cref="PersistenceErrors.NotSupported"/>，而不是容器里找不到服务
    /// </remarks>
    public static IServiceCollection AddUnsupportedSchemaReader(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ISchemaReader, UnsupportedSchemaReader>();
        return services;
    }
}
