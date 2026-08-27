// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Configuration;
using XiHan.Framework.AI.Abstractions.Prompts;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// XiHan AI 根配置测试
/// </summary>
/// <remarks>
/// 根配置的三个坑都在默认值上：配置节名是部署方 appsettings 里的字面量、
/// 子对象必须每实例一份（不能是共享静态）、provider 字典必须大小写不敏感。
/// </remarks>
public class XiHanAiOptionsTests
{
    /// <summary>
    /// 配置节名锁定为 XiHan:AI
    /// </summary>
    /// <remarks>
    /// 这是部署方 appsettings.json 里已经写死的键路径，改动等于让所有既有部署静默失配置，
    /// 因此按字面量而不是按常量引用来断言。
    /// </remarks>
    [Fact]
    public void SectionName_IsStableConfigurationPath()
    {
        Assert.Equal("XiHan:AI", XiHanAiOptions.SectionName);
    }

    /// <summary>
    /// 默认 provider 未指定时为 null
    /// </summary>
    /// <remarks>null 表示「未声明默认」，由解析器 fail-closed 处理，不是「有个叫空串的 provider」。</remarks>
    [Fact]
    public void DefaultProvider_WhenNewInstance_IsNull()
    {
        var options = new XiHanAiOptions();

        Assert.Null(options.DefaultProvider);
    }

    /// <summary>
    /// 子对象与集合默认已初始化，调用方无需判空
    /// </summary>
    [Fact]
    public void Defaults_ForCollectionsAndPipeline_AreInitializedAndEmpty()
    {
        var options = new XiHanAiOptions();

        Assert.NotNull(options.Providers);
        Assert.Empty(options.Providers);
        Assert.NotNull(options.Prompts);
        Assert.Empty(options.Prompts);
        Assert.NotNull(options.Pipeline);
    }

    /// <summary>
    /// 每个实例持有各自的子对象，不共享同一份引用
    /// </summary>
    /// <remarks>
    /// 若默认值被写成静态共享实例，多租户/多次绑定场景下改一处会污染全部，
    /// 这类问题在运行期极难定位，故在此直接锁死。
    /// </remarks>
    [Fact]
    public void Defaults_ForTwoInstances_AreNotSharedReferences()
    {
        var first = new XiHanAiOptions();
        var second = new XiHanAiOptions();

        Assert.NotSame(first.Providers, second.Providers);
        Assert.NotSame(first.Prompts, second.Prompts);
        Assert.NotSame(first.Pipeline, second.Pipeline);
    }

    /// <summary>
    /// provider 字典按名查找大小写不敏感
    /// </summary>
    /// <param name="lookupKey">查找时使用的大小写变体</param>
    /// <remarks>
    /// appsettings 里 provider 名的大小写由人手写，"OpenAI"/"openai"/"OPENAI" 必须命中同一份配置。
    /// </remarks>
    [Theory]
    [InlineData("OpenAI")]
    [InlineData("openai")]
    [InlineData("OPENAI")]
    [InlineData("oPeNaI")]
    public void Providers_WhenLookedUpWithDifferentCasing_ResolvesSameEntry(string lookupKey)
    {
        var options = new XiHanAiOptions();
        options.Providers["OpenAI"] = new AiProviderOptions
        {
            Provider = "OpenAI",
            Model = "gpt-4o-mini"
        };

        Assert.True(options.Providers.TryGetValue(lookupKey, out var resolved));
        Assert.Equal("gpt-4o-mini", resolved!.Model);
    }

    /// <summary>
    /// 大小写不同的同名 provider 视为同一条目而非新增
    /// </summary>
    [Fact]
    public void Providers_WhenSameNameDifferentCasingAdded_OverwritesInsteadOfDuplicating()
    {
        var options = new XiHanAiOptions();
        options.Providers["DeepSeek"] = new AiProviderOptions { Provider = "DeepSeek", Model = "v1" };
        options.Providers["deepseek"] = new AiProviderOptions { Provider = "deepseek", Model = "v2" };

        Assert.Single(options.Providers);
        Assert.Equal("v2", options.Providers["DEEPSEEK"].Model);
    }

    /// <summary>
    /// 管道开关是全局的，取自根配置而非 provider 配置
    /// </summary>
    /// <remarks>Pipeline 挂在根上、AiProviderOptions 上没有同名字段，是「横切开关全局生效」的结构保证。</remarks>
    [Fact]
    public void Pipeline_IsGlobalNotPerProvider()
    {
        var options = new XiHanAiOptions();
        options.Pipeline.EnableGuardrail = true;

        Assert.True(options.Pipeline.EnableGuardrail);
        Assert.Null(typeof(AiProviderOptions).GetProperty(nameof(XiHanAiOptions.Pipeline)));
    }

    /// <summary>
    /// 根配置整体可经 System.Text.Json 往返，嵌套的 provider、管道、提示词均保留
    /// </summary>
    /// <param name="useWebNaming">true 用 Web 驼峰命名策略，false 用默认策略</param>
    /// <remarks>用原大小写的键取值，因为 JSON 往返后字典比较器不保证仍是大小写不敏感的。</remarks>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTrip_WithNestedSections_PreservesEverySection(bool useWebNaming)
    {
        JsonSerializerOptions? serializerOptions = useWebNaming ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : null;
        var source = new XiHanAiOptions
        {
            DefaultProvider = "openai"
        };
        source.Providers["openai"] = new AiProviderOptions
        {
            Provider = "openai",
            Model = "gpt-4o-mini",
            EmbeddingModel = "text-embedding-3-small"
        };
        source.Pipeline.EnableGuardrail = true;
        source.Pipeline.TelemetrySourceName = "XiHan.AI.Test";
        source.Prompts.Add(new AiPromptTemplate
        {
            Name = "code-review",
            Content = "请审查以下代码：{{code}}",
            Version = "v2"
        });

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<XiHanAiOptions>(json, serializerOptions)!;

        Assert.Equal("openai", restored.DefaultProvider);
        Assert.True(restored.Providers.TryGetValue("openai", out var provider));
        Assert.Equal("gpt-4o-mini", provider!.Model);
        Assert.Equal("text-embedding-3-small", provider.EmbeddingModel);
        Assert.True(restored.Pipeline.EnableGuardrail);
        Assert.Equal("XiHan.AI.Test", restored.Pipeline.TelemetrySourceName);
        var prompt = Assert.Single(restored.Prompts);
        Assert.Equal("code-review", prompt.Name);
        Assert.Equal("v2", prompt.Version);
    }

    /// <summary>
    /// 提示词集合是 Options 兜底源，默认为空表示交由 store 实现提供
    /// </summary>
    [Fact]
    public void Prompts_WhenAppended_KeepsInsertionOrder()
    {
        var options = new XiHanAiOptions();
        options.Prompts.Add(new AiPromptTemplate { Name = "first" });
        options.Prompts.Add(new AiPromptTemplate { Name = "second" });

        Assert.Equal(2, options.Prompts.Count);
        Assert.Equal("first", options.Prompts[0].Name);
        Assert.Equal("second", options.Prompts[1].Name);
    }
}
