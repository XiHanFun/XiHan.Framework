// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Plugins.OutOfProcess.Tests.Fixtures;

/// <summary>
/// 挂 Span 监听器的用例
/// </summary>
/// <remarks>
/// ActivitySource 是进程级的：挂着监听器时，同一程序集里别的用例的调用也会开 Span、写给插件的 traceparent 随之改变。这个集合不与其他集合并行
/// </remarks>
[CollectionDefinition(nameof(TelemetryGroup), DisableParallelization = true)]
public sealed class TelemetryGroup;
