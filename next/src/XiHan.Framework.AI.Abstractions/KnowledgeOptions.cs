// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// 知识库选项（<c>XiHan:AI:Knowledge</c>）：默认集合、嵌入维度、切块、检索条数、嵌入 Provider 与增强提示词。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record KnowledgeOptions
{
    /// <summary>
    /// 默认集合名。
    /// </summary>
    public const string DefaultCollection = "knowledge";

    /// <summary>
    /// 默认集合；默认 <c>knowledge</c>。
    /// </summary>
    public string Collection
    {
        get => field;
        init => field = AIIdentifiers.ValidateName(value, nameof(value));
    } = DefaultCollection;

    /// <summary>
    /// 嵌入维度；默认 1536。
    /// </summary>
    public int Dimensions
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The dimensions must be at least 1.");
    } = 1536;

    /// <summary>
    /// 切块大小（字符）；默认 1000。
    /// </summary>
    public int ChunkSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The chunk size must be at least 1.");
    } = 1000;

    /// <summary>
    /// 相邻块的重叠（字符）；默认 100。
    /// </summary>
    public int ChunkOverlap
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The overlap must not be negative.");
    } = 100;

    /// <summary>
    /// 默认检索条数；默认 5。
    /// </summary>
    public int Top
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "Top must be at least 1.");
    } = 5;

    /// <summary>
    /// 嵌入 Provider 名；空表示默认嵌入 Provider。
    /// </summary>
    public string? EmbeddingProvider { get; init; }

    /// <summary>
    /// 提示增强用的提示词模板名（占位符 <c>{context}</c>、<c>{question}</c>）；空表示内置模板。
    /// </summary>
    public string? PromptName { get; init; }

    /// <summary>
    /// 切块选项。
    /// </summary>
    public ChunkingOptions Chunking => new() { MaxChunkSize = ChunkSize, Overlap = ChunkOverlap };
}
