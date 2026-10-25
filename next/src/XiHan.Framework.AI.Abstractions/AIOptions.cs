// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// AI 子系统选项（配置节 <c>XiHan:AI</c>）：默认 Provider、Provider 表、工具名覆盖、护栏、知识库与配置里的提示词。
/// </summary>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AIOptions
{
    /// <summary>
    /// 配置节。
    /// </summary>
    public const string ConfigurationSection = "XiHan:AI";

    /// <summary>
    /// 默认聊天 Provider；空且只有一个 Provider 时取它。
    /// </summary>
    public string? DefaultProvider { get; init; }

    /// <summary>
    /// 默认嵌入 Provider；空时同 <see cref="DefaultProvider"/>。
    /// </summary>
    public string? DefaultEmbeddingProvider { get; init; }

    /// <summary>
    /// Provider 表（名字 → 配置）。
    /// </summary>
    public IReadOnlyDictionary<string, AIProviderOptions> Providers { get; init; } = new Dictionary<string, AIProviderOptions>(StringComparer.Ordinal);

    /// <summary>
    /// 操作工具名覆盖（操作 ID → 工具名）。
    /// </summary>
    public IReadOnlyDictionary<string, string> Tools { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 护栏选项。
    /// </summary>
    public AIGuardrailOptions Guardrails { get; init; } = new();

    /// <summary>
    /// 知识库选项。
    /// </summary>
    public KnowledgeOptions Knowledge { get; init; } = new();

    /// <summary>
    /// 配置里声明的提示词模板。
    /// </summary>
    public IReadOnlyList<AIPromptTemplate> Prompts { get; init; } = [];

    /// <summary>
    /// 实际的默认聊天 Provider 名。
    /// </summary>
    public string? ResolveDefaultProvider() => DefaultProvider ?? (Providers.Count == 1 ? Providers.Keys.First() : null);

    /// <summary>
    /// 实际的默认嵌入 Provider 名。
    /// </summary>
    public string? ResolveDefaultEmbeddingProvider() => DefaultEmbeddingProvider ?? ResolveDefaultProvider();

    /// <summary>
    /// 从配置读取。
    /// </summary>
    public static AIOptions FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(ConfigurationSection);
        var providers = new Dictionary<string, AIProviderOptions>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Providers").GetChildren())
        {
            providers[child.Key] = ReadProvider(child);
        }

        var tools = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Tools").GetChildren())
        {
            if (child.Value is { } name)
            {
                tools[child.Key] = name;
            }
        }

        var prompts = new List<AIPromptTemplate>();
        foreach (var child in section.GetSection("Prompts").GetChildren())
        {
            var template = child["Template"] ?? child.Value;
            if (template is not null)
            {
                prompts.Add(new AIPromptTemplate(child.Key, template, child["Description"]));
            }
        }

        return new AIOptions
        {
            DefaultProvider = section["DefaultProvider"],
            DefaultEmbeddingProvider = section["DefaultEmbeddingProvider"],
            Providers = providers,
            Tools = tools,
            Guardrails = ReadGuardrails(section.GetSection("Guardrails")),
            Knowledge = ReadKnowledge(section.GetSection("Knowledge")),
            Prompts = prompts,
        };
    }

    /// <summary>
    /// 读一个 Provider。
    /// </summary>
    private static AIProviderOptions ReadProvider(IConfigurationSection section)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in section.GetSection("Properties").GetChildren())
        {
            if (child.Value is { } value)
            {
                properties[child.Key] = value;
            }
        }

        var options = new AIProviderOptions(section.Key, section["Kind"] ?? "openai")
        {
            Endpoint = Uri.TryCreate(section["Endpoint"], UriKind.Absolute, out var endpoint) ? endpoint : null,
            ApiKey = section["ApiKey"],
            ChatModel = section["ChatModel"],
            EmbeddingModel = section["EmbeddingModel"],
            Properties = properties,
        };
        if (int.TryParse(section["EmbeddingDimensions"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dimensions))
        {
            options = options with { EmbeddingDimensions = dimensions };
        }

        if (double.TryParse(section["TimeoutSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            options = options with { Timeout = TimeSpan.FromSeconds(seconds) };
        }

        return options;
    }

    /// <summary>
    /// 读护栏选项。
    /// </summary>
    private static AIGuardrailOptions ReadGuardrails(IConfigurationSection section) => new()
    {
        Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled,
        BlockedKeywords = ReadList(section.GetSection("BlockedKeywords")),
        InjectionPatterns = ReadList(section.GetSection("InjectionPatterns")),
        UseBuiltInInjectionHeuristics = !bool.TryParse(section["UseBuiltInInjectionHeuristics"], out var heuristics) || heuristics,
    };

    /// <summary>
    /// 读知识库选项。
    /// </summary>
    private static KnowledgeOptions ReadKnowledge(IConfigurationSection section)
    {
        var options = new KnowledgeOptions { EmbeddingProvider = section["EmbeddingProvider"], PromptName = section["PromptName"] };
        if (section["Collection"] is { } collection)
        {
            options = options with { Collection = collection };
        }

        if (int.TryParse(section["Dimensions"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dimensions))
        {
            options = options with { Dimensions = dimensions };
        }

        if (int.TryParse(section["ChunkSize"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size))
        {
            options = options with { ChunkSize = size };
        }

        if (int.TryParse(section["ChunkOverlap"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var overlap))
        {
            options = options with { ChunkOverlap = overlap };
        }

        if (int.TryParse(section["Top"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var top))
        {
            options = options with { Top = top };
        }

        return options;
    }

    /// <summary>
    /// 读字符串数组节（<c>:0</c>、<c>:1</c>…）。
    /// </summary>
    private static List<string> ReadList(IConfigurationSection section)
        => section.GetChildren().Select(static child => child.Value).Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value!).ToList();
}
