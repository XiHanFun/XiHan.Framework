// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text;
using XiHan.Framework.AI.Abstractions.Prompts;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions.Knowledge;

/// <summary>
/// <see cref="IRagPromptAugmenter"/> 的实现。
/// </summary>
/// <remarks>
/// <see cref="KnowledgeOptions.PromptName"/> 指向的模板（占位符 <c>{context}</c>、<c>{question}</c>）优先，否则内置英文模板。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed class RagPromptAugmenter : IRagPromptAugmenter
{
    /// <summary>
    /// 内置模板。
    /// </summary>
    public const string BuiltInTemplate = "Answer the question using only the knowledge snippets below. If they are not enough, say that no supporting evidence was found instead of guessing. Cite snippets by their number, like [1].\n\n# Knowledge snippets\n{context}\n# Question\n{question}";

    /// <summary>
    /// 选项。
    /// </summary>
    private readonly KnowledgeOptions _options;

    /// <summary>
    /// 提示词目录。
    /// </summary>
    private readonly IAIPromptCatalog _prompts;

    /// <summary>
    /// 创建。
    /// </summary>
    public RagPromptAugmenter(AIOptions options, IAIPromptCatalog prompts)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Knowledge;
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
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
            context.Append('[').Append((index + 1).ToString(CultureInfo.InvariantCulture)).Append("] (document: ").Append(chunk.DocumentId).Append(", chunk: ").Append(chunk.ChunkIndex.ToString(CultureInfo.InvariantCulture)).Append(")\n");
            context.Append(chunk.Text).Append("\n\n");
        }

        var arguments = MessageArguments.Of(("context", context.ToString()), ("question", question));
        // 配置的模板渲染失败（没有占位符之类）时退回内置模板，不让检索结果丢失
        if (_options.PromptName is not null && _prompts.Find(_options.PromptName).TryGetValue(out var template) && template.Render(arguments).TryGetValue(out var rendered))
        {
            return rendered;
        }

        return new AIPromptTemplate("rag", BuiltInTemplate).Render(arguments).ValueOrThrow();
    }
}
