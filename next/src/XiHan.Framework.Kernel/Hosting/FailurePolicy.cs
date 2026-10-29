// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 特性启动失败时宿主的处理策略。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public enum FailurePolicy
{
    /// <summary>
    /// 终止启动，抛出 <see cref="KernelErrors.FeatureStartFailed"/>。
    /// </summary>
    TerminateStartup = 0,

    /// <summary>
    /// 隔离该特性（<see cref="LifecycleState.Failed"/>），继续启动其余特性。
    /// </summary>
    /// <remarks>
    /// 应用不就绪。
    /// </remarks>
    IsolateFeature = 1,
}
