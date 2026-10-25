// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.AI;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 工具目录：<c>AddAITool</c> 显式登记的工具，加上登记表里 <c>Expose.Ai</c> 且非 <c>Internal</c> 的操作桥接成的 <see cref="AIFunction"/>。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAIToolCatalog
{
    /// <summary>
    /// 全部工具的声明（名字、描述、Schema），按名字排序；操作工具是不可调用的声明。
    /// </summary>
    IReadOnlyList<AITool> All();

    /// <summary>
    /// 按名字查声明。
    /// </summary>
    Maybe<AITool> Find(string name);

    /// <summary>
    /// 绑定到调用方上下文的可执行工具：操作工具在调用方的 Baggage、截止时间与区域下执行。
    /// </summary>
    IReadOnlyList<AITool> Bind(InvocationContext caller);
}
