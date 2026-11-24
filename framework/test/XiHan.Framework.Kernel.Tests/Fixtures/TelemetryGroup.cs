// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 要求「此刻没有任何监听器」的用例
/// </summary>
/// <remarks>
/// ActivitySource 与 Meter 是进程级的：别的用例挂着监听器时，「没有监听器」就不成立。这个集合不与其他集合并行
/// </remarks>
[CollectionDefinition(nameof(TelemetryGroup), DisableParallelization = true)]
public sealed class TelemetryGroup;
