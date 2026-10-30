// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// 系统 CPU 的累计时间。
/// </summary>
/// <remarks>
/// 单位由平台决定（Linux 是时钟节拍，Windows 是 100 纳秒），只用来求两次采样之差。
/// </remarks>
/// <param name="Idle">空闲时间。</param>
/// <param name="Total">总时间，含空闲。</param>
internal readonly record struct CpuTimes(ulong Idle, ulong Total);
