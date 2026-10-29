// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 特性与插件共用的生命周期状态。
/// </summary>
/// <remarks>
/// 状态集合与合法迁移是跨语言规范的一部分，也是探针与健康端点的输出词汇。
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public enum LifecycleState
{
    /// <summary>
    /// 已从清单声明，尚未解析依赖。
    /// </summary>
    Declared = 0,

    /// <summary>
    /// 依赖已解析并排序。
    /// </summary>
    Resolved = 1,

    /// <summary>
    /// 服务已注册。
    /// </summary>
    Configured = 2,

    /// <summary>
    /// 已启动，尚未就绪。
    /// </summary>
    Started = 3,

    /// <summary>
    /// 就绪，可以接收调用。
    /// </summary>
    Ready = 4,

    /// <summary>
    /// 可选依赖或绑定失效，仍可服务但健康端点报告降级。
    /// </summary>
    Degraded = 5,

    /// <summary>
    /// 停止接入，排空在途调用。
    /// </summary>
    Draining = 6,

    /// <summary>
    /// 已停止。
    /// </summary>
    Stopped = 7,

    /// <summary>
    /// 任一步失败。
    /// </summary>
    /// <remarks>
    /// 宿主按策略终止启动或隔离该特性。
    /// </remarks>
    Failed = 8,
}
