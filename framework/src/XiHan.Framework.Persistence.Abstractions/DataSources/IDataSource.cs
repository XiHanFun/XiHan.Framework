// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Abstractions.DataSources;

/// <summary>
/// 一个已建的运行时数据源
/// </summary>
/// <remarks>
/// 由 <see cref="IDataSourceProvider"/> 按定义建出，<see cref="DataSourceRegistry"/> 按名缓存。具体的连接对象在 Provider 的实现类型上（如 SqlSugar 的 <c>SqlSugarDataSource.Client</c>）。实现持有需要释放的资源时实现 <see cref="IDisposable"/> 或 <see cref="IAsyncDisposable"/>，由注册表在移除、替换与停止时释放；使用方不要自己释放
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public interface IDataSource
{
    /// <summary>
    /// 数据源名
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 数据库类型
    /// </summary>
    string DatabaseType { get; }
}
