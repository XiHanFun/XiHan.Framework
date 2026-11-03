// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.AI.Abstractions.Guardrails;
using XiHan.Framework.AI.Abstractions.Knowledge;
using XiHan.Framework.AI.Abstractions.Prompts;
using XiHan.Framework.AI.Abstractions.Providers;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.AI.Abstractions;

/// <summary>
/// AI 子系统选项（配置节 <c>XiHan:AI</c>）
/// </summary>
/// <remarks>
/// 默认 Provider、Provider 表、工具名覆盖、护栏、知识库与配置里的提示词。读取配置时值不合法即抛出 <see cref="ArgumentException"/>（启动失败），不按缺省处理
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record AIOptions
{
    /// <summary>
    /// 配置节
    /// </summary>
    public const string ConfigurationSection = "XiHan:AI";

    /// <summary>
    /// 默认聊天 Provider
    /// </summary>
    /// <remarks>
    /// 空且只有一个 Provider 时取它。框架自带的目录由此给出 <see cref="IAIProviderCatalog.DefaultProvider"/>；聊天与知识库只问目录，应用替换了目录时这一项不起作用
    /// </remarks>
    public string? DefaultProvider { get; init; }

    /// <summary>
    /// 默认嵌入 Provider
    /// </summary>
    /// <remarks>
    /// 空时同 <see cref="DefaultProvider"/>。框架自带的目录由此给出 <see cref="IAIProviderCatalog.DefaultEmbeddingProvider"/>
    /// </remarks>
    public string? DefaultEmbeddingProvider { get; init; }

    /// <summary>
    /// Provider 表（名字 → 配置）
    /// </summary>
    public IReadOnlyDictionary<string, AIProviderOptions> Providers { get; init; } = new Dictionary<string, AIProviderOptions>(StringComparer.Ordinal);

    /// <summary>
    /// 操作工具名覆盖（操作 ID → 工具名）
    /// </summary>
    public IReadOnlyDictionary<string, string> Tools { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// 护栏选项
    /// </summary>
    public AIGuardrailOptions Guardrails { get; init; } = new();

    /// <summary>
    /// 知识库选项
    /// </summary>
    public KnowledgeOptions Knowledge { get; init; } = new();

    /// <summary>
    /// 配置里声明的提示词模板
    /// </summary>
    public IReadOnlyList<AIPromptTemplate> Prompts { get; init; } = [];

    /// <summary>
    /// 按配置推出的默认聊天 Provider 名（框架自带目录的 <see cref="IAIProviderCatalog.DefaultProvider"/>）
    /// </summary>
    public string? ResolveDefaultProvider() => DefaultProvider ?? (Providers.Count == 1 ? Providers.Keys.First() : null);

    /// <summary>
    /// 按配置推出的默认嵌入 Provider 名（框架自带目录的 <see cref="IAIProviderCatalog.DefaultEmbeddingProvider"/>）
    /// </summary>
    public string? ResolveDefaultEmbeddingProvider() => DefaultEmbeddingProvider ?? ResolveDefaultProvider();

    /// <summary>
    /// 从配置读取
    /// </summary>
    /// <remarks>
    /// Provider 缺 <c>Kind</c>、<c>Endpoint</c> 不是绝对的 http(s) 地址、数值项不是要求的整数或正数、布尔项不是 <c>true</c> / <c>false</c>、提示词缺模板时抛出 <see cref="ArgumentException"/>
    /// </remarks>
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
            var template = child["Template"] ?? child.Value ?? throw new ArgumentException($"'{child.Path}:Template' is required.", nameof(configuration));
            prompts.Add(new AIPromptTemplate(child.Key, template, child["Description"]));
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
    /// 读一个 Provider
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

        // 种类没有缺省：写漏了不能悄悄当成 openai，把请求与密钥发给官方端点
        var kind = section["Kind"];
        if (string.IsNullOrWhiteSpace(kind))
        {
            throw new ArgumentException($"'{section.Path}:Kind' is required.", nameof(section));
        }

        var options = new AIProviderOptions(section.Key, kind)
        {
            Endpoint = ReadHttpUrl(section, "Endpoint"),
            ApiKey = section["ApiKey"],
            ChatModel = section["ChatModel"],
            EmbeddingModel = section["EmbeddingModel"],
            EmbeddingDimensions = ReadInt(section, "EmbeddingDimensions", minimum: 1),
            Properties = properties,
        };
        if (ReadPositiveNumber(section, "TimeoutSeconds") is { } seconds)
        {
            options = options with { Timeout = TimeSpan.FromSeconds(seconds) };
        }

        return options;
    }

    /// <summary>
    /// 读护栏选项
    /// </summary>
    private static AIGuardrailOptions ReadGuardrails(IConfigurationSection section) => new()
    {
        Enabled = ReadBool(section, "Enabled") ?? false,
        BlockedKeywords = ReadList(section.GetSection("BlockedKeywords")),
        InjectionPatterns = ReadList(section.GetSection("InjectionPatterns")),
        UseBuiltInInjectionHeuristics = ReadBool(section, "UseBuiltInInjectionHeuristics") ?? true,
    };

    /// <summary>
    /// 读知识库选项
    /// </summary>
    private static KnowledgeOptions ReadKnowledge(IConfigurationSection section)
    {
        var options = new KnowledgeOptions { EmbeddingProvider = section["EmbeddingProvider"], PromptName = section["PromptName"] };
        if (section["Collection"] is { } collection)
        {
            options = options with { Collection = collection };
        }

        if (ReadInt(section, "Dimensions", minimum: 1) is { } dimensions)
        {
            options = options with { Dimensions = dimensions };
        }

        if (ReadInt(section, "ChunkSize", minimum: 1) is { } size)
        {
            options = options with { ChunkSize = size };
        }

        if (ReadInt(section, "ChunkOverlap", minimum: 0) is { } overlap)
        {
            options = options with { ChunkOverlap = overlap };
        }

        if (ReadInt(section, "Top", minimum: 1) is { } top)
        {
            options = options with { Top = top };
        }

        return options;
    }

    /// <summary>
    /// 读取整数键
    /// </summary>
    /// <remarks>
    /// 缺省返回 <see langword="null"/>，不是整数或小于 <paramref name="minimum"/> 抛出 <see cref="ArgumentException"/>
    /// </remarks>
    private static int? ReadInt(IConfigurationSection section, string key, int minimum)
    {
        var raw = section[key];
        if (raw is null)
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= minimum
            ? value
            : throw new ArgumentException($"'{section.Path}:{key}' must be an integer of at least {minimum}, but the value is '{raw}'.", nameof(section));
    }

    /// <summary>
    /// 读取正数键
    /// </summary>
    /// <remarks>
    /// 缺省返回 <see langword="null"/>，不是有限的正数抛出 <see cref="ArgumentException"/>
    /// </remarks>
    private static double? ReadPositiveNumber(IConfigurationSection section, string key)
    {
        var raw = section[key];
        if (raw is null)
        {
            return null;
        }

        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value > 0
            ? value
            : throw new ArgumentException($"'{section.Path}:{key}' must be a positive number, but the value is '{raw}'.", nameof(section));
    }

    /// <summary>
    /// 读取布尔键
    /// </summary>
    /// <remarks>
    /// 缺省返回 <see langword="null"/>，不是 <c>true</c> / <c>false</c> 抛出 <see cref="ArgumentException"/>
    /// </remarks>
    private static bool? ReadBool(IConfigurationSection section, string key)
    {
        var raw = section[key];
        if (raw is null)
        {
            return null;
        }

        return bool.TryParse(raw, out var value) ? value : throw new ArgumentException($"'{section.Path}:{key}' must be 'true' or 'false', but the value is '{raw}'.", nameof(section));
    }

    /// <summary>
    /// 读取 http(s) 地址键
    /// </summary>
    /// <remarks>
    /// 缺省或空串返回 <see langword="null"/>，不是绝对的 http 或 https 地址抛出 <see cref="ArgumentException"/>
    /// </remarks>
    private static Uri? ReadHttpUrl(IConfigurationSection section, string key)
    {
        var raw = section[key];
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        return Uri.TryCreate(raw, UriKind.Absolute, out var value) && AIProviderOptions.IsHttpUrl(value)
            ? value
            : throw new ArgumentException($"'{section.Path}:{key}' must be an absolute http or https URL, but the value is '{raw}'.", nameof(section));
    }

    /// <summary>
    /// 读字符串数组节（<c>:0</c>、<c>:1</c>…）
    /// </summary>
    private static List<string> ReadList(IConfigurationSection section)
        => section.GetChildren().Select(static child => child.Value).Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value!).ToList();
}
