// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 把检索到的块拼进问题，得到交给模型的提示。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IRagPromptAugmenter
{
    /// <summary>
    /// 增强。
    /// </summary>
    /// <remarks>
    /// 没有块时原样返回问题。配置了 <see cref="KnowledgeOptions.PromptName"/> 时用它指向的模板：找不到 → <see cref="AIErrors.PromptNotFound"/>，渲染失败（模板引用了 <c>{context}</c>、<c>{question}</c> 以外的占位符）→ <see cref="AIErrors.RequestInvalid"/>，不退回内置模板。没有配置时用内置英文模板。
    /// </remarks>
    Result<string> Augment(string question, IReadOnlyList<RetrievedChunk> chunks);
}
