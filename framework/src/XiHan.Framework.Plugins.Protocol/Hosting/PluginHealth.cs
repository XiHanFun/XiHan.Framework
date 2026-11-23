// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.Protocol.Mapping;
using XiHan.Framework.Plugins.Protocol.V1;
using Error = XiHan.Framework.Primitives.Error;
using LifecycleState = XiHan.Framework.Primitives.LifecycleState;
using ProtoLifecycleState = XiHan.Framework.Plugins.Protocol.V1.LifecycleState;

namespace XiHan.Framework.Plugins.Protocol.Hosting;

/// <summary>
/// 健康应答到宿主一侧状态的映射
/// </summary>
/// <remarks>
/// 进程外与 Wasm 宿主共用（<c>spec/plugin/lifecycle.md</c> §1、§4，ADR-0260）。只给出应答说的是什么；插件自报 Failed 之后怎么办是载体的事：进程外终止进程交给退出监视，Wasm 按崩溃重建实例
/// </remarks>
internal static class PluginHealth
{
    /// <summary>
    /// 健康条目名前缀：每个插件是宿主的健康条目 <c>plugins.&lt;清单 ID&gt;</c>
    /// </summary>
    public const string EntryPrefix = "plugins.";

    /// <summary>
    /// 一次健康应答要让插件进入的状态与随它记下的错误
    /// </summary>
    /// <remarks>
    /// 状态为空表示不改：插件自报的只能是 Started、Ready、Degraded、Draining、Failed，其余由宿主裁决。自报 Ready 而有健康项 Degraded / Unhealthy 时按 Degraded；错误只随 Failed 与 Degraded 记下
    /// </remarks>
    /// <param name="health">健康应答</param>
    /// <param name="manifestId">插件的清单 ID</param>
    public static (LifecycleState? State, Error? Error) Read(HealthResponse health, string manifestId)
    {
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(manifestId);
        var error = health.Error is { } reported ? PluginErrorMapper.FromProto(reported, manifestId) : null;
        switch (health.State)
        {
            case ProtoLifecycleState.Failed:
                return (LifecycleState.Failed, error);
            case ProtoLifecycleState.Degraded:
                return (LifecycleState.Degraded, error);
            case ProtoLifecycleState.Ready:
                // 任一健康项不健康，插件整体就是 Degraded（lifecycle.md §1）
                return health.Items.Any(static item => item.Status is HealthStatus.Degraded or HealthStatus.Unhealthy)
                    ? (LifecycleState.Degraded, error)
                    : (LifecycleState.Ready, null);
            case ProtoLifecycleState.Started:
                return (LifecycleState.Started, null);
            case ProtoLifecycleState.Draining:
                return (LifecycleState.Draining, null);
            default:
                // 其余状态由宿主裁决，插件自报无效
                return (null, null);
        }
    }
}
