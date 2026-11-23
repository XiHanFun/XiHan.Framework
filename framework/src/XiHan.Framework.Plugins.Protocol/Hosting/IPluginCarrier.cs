// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 承载插件的载体（子进程、Wasm 实例）交给 <see cref="PluginLifecycle"/> 的部分
/// </summary>
/// <remarks>
/// 生命周期组件只管状态与退避的节奏（ADR-0260）；怎样把插件重新带起来、什么时候不该再试，以及各载体自己措辞的日志，由运行时经这个接口给出
/// </remarks>
internal interface IPluginCarrier
{
    /// <summary>
    /// 不再启动也不再重启：已显式停止，或按发现时被拒处理
    /// </summary>
    bool IsStopped { get; }

    /// <summary>
    /// 一次重试：把插件重新带到可服务
    /// </summary>
    /// <remarks>
    /// 进程外是重新启动进程并握手、配置、启动（首次握手还没成功过的先探测启动并登记声明），Wasm 是重建实例并重走握手、配置、启动。失败由调用方按退避再试；期间被停止时可以抛出取消
    /// </remarks>
    /// <param name="cancellationToken">显式停止时取消</param>
    Task<Result> RecoverAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 插件进入了一个状态
    /// </summary>
    /// <param name="state">进入的状态</param>
    void OnStateEntered(LifecycleState state);

    /// <summary>
    /// 排了一次重试
    /// </summary>
    /// <param name="delay">退避时长</param>
    /// <param name="attempt">时间窗内的第几次，从 1 起</param>
    void OnRestartScheduled(TimeSpan delay, int attempt);

    /// <summary>
    /// 重试成功
    /// </summary>
    /// <param name="restarts">累计自动重启次数</param>
    void OnRestarted(int restarts);

    /// <summary>
    /// 时间窗内的重试次数用完，插件保持 Failed
    /// </summary>
    /// <param name="limit">次数上限</param>
    /// <param name="window">时间窗</param>
    void OnRestartLimitReached(int limit, TimeSpan window);
}
