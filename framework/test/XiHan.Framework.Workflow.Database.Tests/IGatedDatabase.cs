// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Testing;

namespace XiHan.Framework.Workflow.Database.Tests;

/// <summary>
/// 一个按环境变量门控的真实数据库：每个用例临时建出，释放时停宿主并删除
/// </summary>
/// <remarks>
/// PostgreSQL、SQL Server、MySQL、Oracle 四个夹具都实现它，在四种库上跑同一份用例的测试类（如 <see cref="ServerWorkflowBackfillTests"/>）只经它装配宿主
/// </remarks>
internal interface IGatedDatabase : IAsyncDisposable
{
    /// <summary>
    /// 启动宿主：业务库绑定 <c>db</c>，开启 <c>InitTables</c>，关掉后台轮询
    /// </summary>
    /// <remarks>
    /// 轮询周期设成 1 小时，由用例主动轮询；实例锁的等待上限 2 秒
    /// </remarks>
    /// <param name="configuration">追加或覆盖的配置</param>
    /// <param name="services">追加的服务登记</param>
    /// <param name="configure">追加的特性装配</param>
    /// <returns>已启动的宿主，释放数据库时一并停止</returns>
    Task<TestHost> StartHostAsync(IEnumerable<KeyValuePair<string, string?>>? configuration = null, Action<IServiceCollection>? services = null, Action<AppBuilder>? configure = null);
}
