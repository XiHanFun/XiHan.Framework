// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Resilience;

/// <summary>
/// 熔断器状态。
/// </summary>
internal enum CircuitState
{
    /// <summary>
    /// 关闭：正常放行，统计连续失败。
    /// </summary>
    Closed = 0,

    /// <summary>
    /// 打开：拒绝调用，直到到期。
    /// </summary>
    Open = 1,

    /// <summary>
    /// 半开：放一次试探，成功关闭、失败重新打开。
    /// </summary>
    HalfOpen = 2,
}
