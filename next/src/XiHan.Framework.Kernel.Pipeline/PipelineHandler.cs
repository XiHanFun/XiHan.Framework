// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Pipeline;

/// <summary>
/// 管道处理器。
/// 表示管道中的一个处理步骤。
/// </summary>
public delegate Task PipelineHandler(PipelineContext context);
