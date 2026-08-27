// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Configuration;

namespace XiHan.Framework.AI.Abstractions.Tests;

/// <summary>
/// 单个 AI Provider 配置测试
/// </summary>
/// <remarks>
/// 这份 DTO 既是 appsettings 的绑定目标，也是应用层 store（DB）的读写载体，
/// 因此关注两件事：默认值区分「未配置」与「空」，以及全字段能经 JSON 无损往返。
/// </remarks>
public class AiProviderOptionsTests
{
    /// <summary>
    /// 标识类字段默认为空串而非 null
    /// </summary>
    /// <remarks>
    /// Provider 与 Model 是不可空 string，绑定缺失时得到空串；
    /// 调用方据此用「空串即未配置」判断，不必再做 null 检查。
    /// </remarks>
    [Fact]
    public void Defaults_ForRequiredIdentityFields_AreEmptyStrings()
    {
        var options = new AiProviderOptions();

        Assert.Equal(string.Empty, options.Provider);
        Assert.Equal(string.Empty, options.Model);
    }

    /// <summary>
    /// 可选字段默认为 null，表示「用 provider/模型默认值」
    /// </summary>
    /// <remarks>
    /// 这些字段必须是 null 而不是 0：0 温度、0 超时、0 最大输出都是合法但危险的取值，
    /// 用 null 才能把「没配」和「配成 0」区分开。
    /// </remarks>
    [Fact]
    public void Defaults_ForOptionalFields_AreNull()
    {
        var options = new AiProviderOptions();

        Assert.Null(options.ApiKey);
        Assert.Null(options.BaseUrl);
        Assert.Null(options.EmbeddingModel);
        Assert.Null(options.MaxOutputTokens);
        Assert.Null(options.Temperature);
        Assert.Null(options.TimeoutSeconds);
        Assert.Null(options.ExtraJson);
    }

    /// <summary>
    /// 可选字段允许显式赋 null（可空性由编译期保证）
    /// </summary>
    /// <remarks>
    /// 这里赋 null 本身就是断言：若哪天把某个字段改成不可空，本方法直接编译失败，
    /// 比运行期断言更早暴露契约收紧。
    /// </remarks>
    [Fact]
    public void OptionalFields_WhenAssignedNull_RemainNullable()
    {
        var options = new AiProviderOptions
        {
            ApiKey = null,
            BaseUrl = null,
            EmbeddingModel = null,
            MaxOutputTokens = null,
            Temperature = null,
            TimeoutSeconds = null,
            ExtraJson = null
        };

        Assert.Null(options.ApiKey);
        Assert.Null(options.ExtraJson);
    }

    /// <summary>
    /// 嵌入模型为空即表示该 provider 不支持嵌入
    /// </summary>
    /// <remarks>会话模型已配而嵌入模型仍为 null 是合法状态，RAG 侧据此跳过该 provider。</remarks>
    [Fact]
    public void EmbeddingModel_WhenOnlyChatModelConfigured_StaysNull()
    {
        var options = new AiProviderOptions
        {
            Provider = "deepseek",
            Model = "deepseek-chat"
        };

        Assert.Equal("deepseek-chat", options.Model);
        Assert.Null(options.EmbeddingModel);
    }

    /// <summary>
    /// 全字段可经 System.Text.Json 往返且值不丢失
    /// </summary>
    /// <param name="useWebNaming">true 用 Web 驼峰命名策略，false 用默认策略</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JsonRoundTrip_WithEveryFieldSet_PreservesValues(bool useWebNaming)
    {
        JsonSerializerOptions? serializerOptions = useWebNaming ? new JsonSerializerOptions(JsonSerializerDefaults.Web) : null;
        var source = new AiProviderOptions
        {
            Provider = "openai",
            ApiKey = "sk-secret",
            BaseUrl = "https://api.example.com/v1",
            Model = "gpt-4o-mini",
            EmbeddingModel = "text-embedding-3-small",
            MaxOutputTokens = 2048,
            Temperature = 0.5f,
            TimeoutSeconds = 30,
            ExtraJson = "{\"top_k\":40}"
        };

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<AiProviderOptions>(json, serializerOptions)!;

        Assert.Equal("openai", restored.Provider);
        Assert.Equal("sk-secret", restored.ApiKey);
        Assert.Equal("https://api.example.com/v1", restored.BaseUrl);
        Assert.Equal("gpt-4o-mini", restored.Model);
        Assert.Equal("text-embedding-3-small", restored.EmbeddingModel);
        Assert.Equal(2048, restored.MaxOutputTokens);
        Assert.Equal(30, restored.TimeoutSeconds);
        Assert.Equal("{\"top_k\":40}", restored.ExtraJson);

        float? expectedTemperature = 0.5f;
        Assert.Equal(expectedTemperature, restored.Temperature);
    }

    /// <summary>
    /// 未配置的可选字段经 JSON 往返后仍为 null
    /// </summary>
    /// <remarks>
    /// 防止序列化把 null 数值字段还原成 0——那会让「没配超时」变成「超时 0 秒」，
    /// 是最容易被忽略的一类配置事故。
    /// </remarks>
    [Fact]
    public void JsonRoundTrip_WithOnlyRequiredFields_KeepsOptionalFieldsNull()
    {
        var source = new AiProviderOptions
        {
            Provider = "ollama",
            Model = "qwen2.5"
        };

        var json = JsonSerializer.Serialize(source);
        var restored = JsonSerializer.Deserialize<AiProviderOptions>(json)!;

        Assert.Equal("ollama", restored.Provider);
        Assert.Equal("qwen2.5", restored.Model);
        Assert.Null(restored.MaxOutputTokens);
        Assert.Null(restored.Temperature);
        Assert.Null(restored.TimeoutSeconds);
        Assert.Null(restored.ApiKey);
        Assert.Null(restored.BaseUrl);
    }

    /// <summary>
    /// 扩展参数以原始 JSON 字符串承载，序列化时不被二次解析
    /// </summary>
    /// <remarks>
    /// ExtraJson 是 string 而非 JsonElement，往返后必须逐字相等；
    /// 若哪天改成结构化类型，本用例会失败，提醒调用方同步改造。
    /// </remarks>
    [Fact]
    public void ExtraJson_WhenRoundTripped_StaysVerbatimString()
    {
        var raw = "{\"a\":1,\"b\":[true,null]}";
        var source = new AiProviderOptions
        {
            ExtraJson = raw
        };

        var restored = JsonSerializer.Deserialize<AiProviderOptions>(JsonSerializer.Serialize(source))!;

        Assert.Equal(raw, restored.ExtraJson);
    }
}
