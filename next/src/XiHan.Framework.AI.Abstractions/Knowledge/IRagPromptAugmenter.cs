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
    /// 没有块时原样返回问题。配置了 <see cref="KnowledgeOptions.PromptName"/> 时用它指向的模板（占位符 <c>{context}</c>、<c>{question}</c>），否则用内置英文模板。模板在启动时校验（找不到或引用了别的占位符即启动失败），增强本身不会失败。
    /// </remarks>
    string Augment(string question, IReadOnlyList<RetrievedChunk> chunks);
}
