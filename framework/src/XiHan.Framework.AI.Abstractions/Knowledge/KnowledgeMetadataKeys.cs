// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 知识库块元数据里框架认识的键
/// </summary>
/// <remarks>
/// 摄入时放进 <see cref="KnowledgeIngestRequest.Metadata"/>，随块存进向量库、检索时回到 <see cref="RetrievedChunk.Metadata"/>；<see cref="IRagPromptAugmenter"/> 用它们写片段的来源。其余键框架不解释，只用于检索过滤
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class KnowledgeMetadataKeys
{
    /// <summary>
    /// 文档标题，提示增强时优先作为片段的来源
    /// </summary>
    public const string Title = "title";

    /// <summary>
    /// 文档的出处（文件名、地址等），没有标题时作为片段的来源
    /// </summary>
    public const string Source = "source";
}
