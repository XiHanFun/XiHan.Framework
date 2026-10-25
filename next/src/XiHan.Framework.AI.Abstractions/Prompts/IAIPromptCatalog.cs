// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Prompts;

/// <summary>
/// 提示词目录：<c>AddAIPrompt</c> 的显式登记（优先）与配置 <c>XiHan:AI:Prompts</c> 的合并。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IAIPromptCatalog
{
    /// <summary>
    /// 按名字查。
    /// </summary>
    Maybe<AIPromptTemplate> Find(string name);

    /// <summary>
    /// 全部模板，按名字排序。
    /// </summary>
    IReadOnlyList<AIPromptTemplate> All();
}
