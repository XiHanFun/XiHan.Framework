// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道委托。表示管道中的一个处理步骤。
/// </summary>
#pragma warning disable CA1711

public delegate Task PipelineDelegate(PipelineContext context);

#pragma warning restore CA1711
