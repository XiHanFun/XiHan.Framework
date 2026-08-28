// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.AI.Abstractions.Prompts;

namespace XiHan.Framework.AI.Abstractions.Tests.Prompts;

/// <summary>
/// AI 提示词模板测试
/// </summary>
/// <remarks>
/// 模板同时经 appsettings 绑定与应用层 store 落库两条路，
/// 关键契约是 Version 用 null 表示「当前/最新」——不能被换成空串或 "latest" 这类魔法值。
/// </remarks>
public class AiPromptTemplateTests
{
    /// <summary>
    /// 标识与正文默认为空串而非 null
    /// </summary>
    [Fact]
    public void Defaults_ForNameAndContent_AreEmptyStrings()
    {
        var template = new AiPromptTemplate();

        Assert.Equal(string.Empty, template.Name);
        Assert.Equal(string.Empty, template.Content);
    }

    /// <summary>
    /// 版本与说明默认为 null
    /// </summary>
    /// <remarks>
    /// Version 为 null 是「取当前版本」的约定值，与 Name/Content 的空串默认刻意不同：
    /// 若这里也变成空串，store 侧「按版本精确匹配」就会退化成匹配一个不存在的空版本。
    /// </remarks>
    [Fact]
    public void Defaults_ForVersionAndDescription_AreNull()
    {
        var template = new AiPromptTemplate();

        Assert.Null(template.Version);
        Assert.Null(template.Description);
    }

    /// <summary>
    /// 正文原样保留占位符，不在 DTO 层做任何渲染或转义
    /// </summary>
    /// <remarks>渲染由上层渲染器负责；DTO 若擅自处理占位符，模板将无法跨渲染引擎复用。</remarks>
    [Fact]
    public void Content_WithPlaceholders_IsStoredVerbatim()
    {
        var raw = "你是{{role}}，请处理：{{ input }}\n保持 {braces} 原样。";
        var template = new AiPromptTemplate
        {
            Name = "demo",
            Content = raw
        };

        Assert.Equal(raw, template.Content);
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
        var source = new AiPromptTemplate
        {
            Name = "code-review",
            Content = "请审查以下代码：{{code}}",
            Version = "v3",
            Description = "代码审查提示词"
        };

        var json = JsonSerializer.Serialize(source, serializerOptions);
        var restored = JsonSerializer.Deserialize<AiPromptTemplate>(json, serializerOptions)!;

        Assert.Equal("code-review", restored.Name);
        Assert.Equal("请审查以下代码：{{code}}", restored.Content);
        Assert.Equal("v3", restored.Version);
        Assert.Equal("代码审查提示词", restored.Description);
    }

    /// <summary>
    /// 未指定版本的模板往返后版本仍为 null
    /// </summary>
    /// <remarks>防止序列化把「当前版本」还原成空串，那会让按版本查询多出一条永远查不到的分支。</remarks>
    [Fact]
    public void JsonRoundTrip_WithoutVersion_KeepsVersionNull()
    {
        var source = new AiPromptTemplate
        {
            Name = "greeting",
            Content = "你好"
        };

        var restored = JsonSerializer.Deserialize<AiPromptTemplate>(JsonSerializer.Serialize(source))!;

        Assert.Equal("greeting", restored.Name);
        Assert.Null(restored.Version);
        Assert.Null(restored.Description);
    }
}
