// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.DataSources;

/// <summary>
/// 按定义建运行时数据源的 Provider（持久化 Provider 各登记一个，单例）
/// </summary>
/// <remarks>
/// <see cref="DataSourceRegistry"/> 取第一个 <see cref="Supports"/> 该数据库类型的 Provider。经 <see cref="DataSourceServiceCollectionExtensions.AddDataSourceProvider"/> 登记
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IDataSourceProvider
{
    /// <summary>
    /// Provider 名（如 <c>sqlsugar</c>），进健康项的数据
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 是否认得该数据库类型（不区分大小写）
    /// </summary>
    bool Supports(string databaseType);

    /// <summary>
    /// 按定义建数据源
    /// </summary>
    /// <remarks>
    /// 不连库：连不上的库照样建出，使用与探测时报告。连接串格式不合法时抛出 <see cref="ArgumentException"/>（消息先经 <see cref="DataSourceDefinition.Redact"/> 脱敏），注册表翻译为 <see cref="PersistenceErrors.DataSourceInvalid"/>
    /// </remarks>
    ValueTask<IDataSource> CreateAsync(DataSourceDefinition definition, CancellationToken cancellationToken);

    /// <summary>
    /// 打开一次连接再关闭
    /// </summary>
    /// <remarks>
    /// 可用返回成功；连不上返回 <see cref="PersistenceErrors.DataSourceUnavailable"/>（参数 <c>name</c>、<c>reason</c>，原因已脱敏）。取消时抛出 <see cref="OperationCanceledException"/>。健康探测与测试连接都经它
    /// </remarks>
    ValueTask<Result> ProbeAsync(IDataSource source, CancellationToken cancellationToken);
}
