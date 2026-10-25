// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 把检索到的块拼进问题，得到交给模型的提示。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IRagPromptAugmenter
{
    /// <summary>
    /// 增强；没有块时原样返回问题。
    /// </summary>
    string Augment(string question, IReadOnlyList<RetrievedChunk> chunks);
}
