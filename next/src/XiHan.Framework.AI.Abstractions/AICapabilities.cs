// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// AI 子系统的能力 ID（RFC-0022）。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public static class AICapabilities
{
    /// <summary>
    /// 聊天：按名字解析 <c>IChatClient</c>。
    /// </summary>
    public const string Chat = "ai.chat";

    /// <summary>
    /// 嵌入：按名字解析 <c>IEmbeddingGenerator</c>。
    /// </summary>
    public const string Embedding = "ai.embedding";

    /// <summary>
    /// 工具目录：显式登记的工具与 <c>Expose.Ai</c> 的操作。
    /// </summary>
    public const string Tools = "ai.tools";

    /// <summary>
    /// 知识库：切块、嵌入、向量存取、检索与提示增强。
    /// </summary>
    public const string Knowledge = "ai.knowledge";

    /// <summary>
    /// 能力版本。
    /// </summary>
    public const int Version = 1;
}
