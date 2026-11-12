// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// 知识库选项（<c>XiHan:AI:Knowledge</c>）
/// </summary>
/// <remarks>
/// 默认集合、嵌入维度、切块、检索条数、嵌入 Provider 与增强提示词
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record KnowledgeOptions
{
    /// <summary>
    /// 默认集合名
    /// </summary>
    public const string DefaultCollection = "knowledge";

    /// <summary>
    /// 默认集合
    /// </summary>
    /// <remarks>
    /// 默认 <c>knowledge</c>。请求没有指明集合时，不带租户的调用用它，带租户的调用用 <see cref="DefaultCollectionFor"/> 派生出的租户集合（ADR-0210）
    /// </remarks>
    public string Collection
    {
        get => field;
        init => field = AIIdentifiers.ValidateName(value, nameof(value));
    } = DefaultCollection;

    /// <summary>
    /// 一次调用在请求没有指明集合时用的集合
    /// </summary>
    /// <remarks>
    /// 不带租户时是 <see cref="Collection"/>；带租户时是 <c>&lt;Collection&gt;.&lt;租户 ID&gt;</c>，各租户的知识互不可见（ADR-0210）。拼出的名字超过 AI 名字的文法（最长 64 个字符）时无值，调用方要显式指明集合
    /// </remarks>
    public Maybe<string> DefaultCollectionFor(string? tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return Collection;
        }

        var scoped = $"{Collection}.{tenantId}";
        return AIIdentifiers.IsValidName(scoped) ? scoped : default(Maybe<string>);
    }

    /// <summary>
    /// 嵌入维度
    /// </summary>
    /// <remarks>
    /// 默认 1536
    /// </remarks>
    public int Dimensions
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The dimensions must be at least 1.");
    } = 1536;

    /// <summary>
    /// 切块大小（字符）
    /// </summary>
    /// <remarks>
    /// 默认 1000
    /// </remarks>
    public int ChunkSize
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The chunk size must be at least 1.");
    } = 1000;

    /// <summary>
    /// 相邻块的重叠（字符）
    /// </summary>
    /// <remarks>
    /// 默认 100
    /// </remarks>
    public int ChunkOverlap
    {
        get => field;
        init => field = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The overlap must not be negative.");
    } = 100;

    /// <summary>
    /// 默认检索条数
    /// </summary>
    /// <remarks>
    /// 默认 5
    /// </remarks>
    public int Top
    {
        get => field;
        init => field = value is >= 1 and <= KnowledgeSearch.MaxTop ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"Top must be between 1 and {KnowledgeSearch.MaxTop}.");
    } = 5;

    /// <summary>
    /// 嵌入 Provider 名
    /// </summary>
    /// <remarks>
    /// 空表示目录的默认嵌入 Provider（<see cref="Providers.IAIProviderCatalog.DefaultEmbeddingProvider"/>）；请求写了 Provider 时以请求为准
    /// </remarks>
    public string? EmbeddingProvider { get; init; }

    /// <summary>
    /// 提示增强用的提示词模板名（占位符 <c>{context}</c>、<c>{question}</c>）
    /// </summary>
    /// <remarks>
    /// 空表示内置模板。启动时校验：模板不存在（<c>AI_NOTFOUND_002</c>）或引用了这两个以外的占位符（<c>AI_VALIDATION_001</c>）即启动失败
    /// </remarks>
    public string? PromptName { get; init; }

    /// <summary>
    /// 切块选项
    /// </summary>
    public ChunkingOptions Chunking => new() { MaxChunkSize = ChunkSize, Overlap = ChunkOverlap };
}
