// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 把文档正文切成块；默认 <see cref="FixedWindowChunkingStrategy"/>，应用可替换。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public interface IChunkingStrategy
{
    /// <summary>
    /// 切块；空白文本返回空列表。
    /// </summary>
    IReadOnlyList<string> Chunk(string text, ChunkingOptions options);
}
