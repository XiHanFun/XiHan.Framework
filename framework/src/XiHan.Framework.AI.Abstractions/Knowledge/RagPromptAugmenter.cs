// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using XiHan.Framework.AI.Abstractions.Prompts;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// <see cref="IRagPromptAugmenter"/> 的实现
/// </summary>
/// <remarks>
/// 创建时就定下模板：配置了 <see cref="KnowledgeOptions.PromptName"/> 就从提示词目录取它指向的模板，并用代表性参数试渲染一次；找不到或渲染不了抛出 <see cref="FrameworkException"/>，宿主启动失败（AI 的宿主特性启动时解析本服务）。没有配置用内置英文模板。之后增强只用这份模板，不再查目录，因而不会失败
/// <c>{context}</c> 里每个片段一段：先是 <c>[序号] Source: 来源</c> 一行，再是片段文本。来源取元数据的 <see cref="KnowledgeMetadataKeys.Title"/>，没有或为空白取 <see cref="KnowledgeMetadataKeys.Source"/>，都没有取文档 ID（ADR-0116）
/// </remarks>
internal sealed class RagPromptAugmenter : IRagPromptAugmenter
{
    /// <summary>
    /// 内置模板
    /// </summary>
    public const string BuiltInTemplate = "Answer the question using only the knowledge snippets below. If they are not enough, say that no supporting evidence was found instead of guessing. Cite snippets by their number, like [1].\n\n# Knowledge snippets\n{context}\n# Question\n{question}";

    /// <summary>
    /// 增强用的模板
    /// </summary>
    /// <remarks>
    /// 已确认只引用 <c>{context}</c> 与 <c>{question}</c>
    /// </remarks>
    private readonly AIPromptTemplate _template;

    /// <summary>
    /// 创建，并校验配置的模板
    /// </summary>
    public RagPromptAugmenter(AIOptions options, IAIPromptCatalog prompts)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(prompts);
        _template = Resolve(options.Knowledge.PromptName, prompts);
    }

    /// <inheritdoc />
    public string Augment(string question, IReadOnlyList<RetrievedChunk> chunks)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(chunks);
        if (chunks.Count == 0)
        {
            return question;
        }

        var context = new StringBuilder();
        for (var index = 0; index < chunks.Count; index++)
        {
            var chunk = chunks[index];
            context.Append('[').Append((index + 1).ToString(CultureInfo.InvariantCulture)).Append("] Source: ").Append(SourceOf(chunk)).Append('\n');
            context.Append(chunk.Text).Append("\n\n");
        }

        // 模板只引用这两个参数（创建时已试渲染），渲染失败只可能是缺参数，这里两个都给了
        return _template.Render(Arguments(context.ToString(), question)).ValueOrThrow();
    }

    /// <summary>
    /// 定下增强用的模板
    /// </summary>
    /// <remarks>
    /// 配置的模板找不到 → <see cref="AIErrors.PromptNotFound"/>，引用了 <c>{context}</c>、<c>{question}</c> 以外的占位符 → <see cref="AIErrors.RequestInvalid"/>，都以 <see cref="FrameworkException"/> 抛出，不退回内置模板
    /// </remarks>
    private static AIPromptTemplate Resolve(string? name, IAIPromptCatalog prompts)
    {
        if (name is null)
        {
            return new AIPromptTemplate("rag", BuiltInTemplate);
        }

        if (!prompts.Find(name).TryGetValue(out var template))
        {
            throw new FrameworkException(AIErrors.PromptNotFound, string.Format(CultureInfo.InvariantCulture, "The knowledge prompt '{0}' set by '{1}:Knowledge:PromptName' is not registered; add '{1}:Prompts:{0}:Template' or register it with 'AddAIPrompt'.", name, AIOptions.ConfigurationSection));
        }

        // 用代表性参数试渲染：增强时只提供这两个参数，这里渲染得出来，运行时就渲染得出来
        if (template.Render(Arguments("[1] Source: sample\nsample\n\n", "sample")).IsFailure)
        {
            var extra = template.Placeholders.Where(static placeholder => placeholder is not ("context" or "question"));
            throw new FrameworkException(AIErrors.RequestInvalid, string.Format(CultureInfo.InvariantCulture, "The knowledge prompt '{0}' may only use the placeholders 'context' and 'question', but it also uses '{1}'.", name, string.Join("', '", extra)));
        }

        return template;
    }

    /// <summary>
    /// 片段的来源
    /// </summary>
    /// <remarks>
    /// 文档 ID 是兜底：多半是 Guid，模型拿它说不清依据。来源只占一行，值里的换行换成空格，否则后面的文本分不清哪里开始
    /// </remarks>
    private static string SourceOf(RetrievedChunk chunk)
        => (Label(chunk, KnowledgeMetadataKeys.Title) ?? Label(chunk, KnowledgeMetadataKeys.Source) ?? chunk.DocumentId).ReplaceLineEndings(" ");

    /// <summary>
    /// 元数据里非空白的值
    /// </summary>
    private static string? Label(RetrievedChunk chunk, string key)
        => chunk.Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// 增强的模板参数
    /// </summary>
    private static IReadOnlyDictionary<string, object?> Arguments(string context, string question) => MessageArguments.Of(("context", context), ("question", question));
}
