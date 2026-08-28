// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Tests.Engines;

/// <summary>
/// <see cref="DefaultTemplateEngineExtensions"/> 字符串、字典与对象扩展入口的测试
/// </summary>
/// <remarks>
/// 这些扩展共享一个静态默认引擎实例，测试只走渲染与校验路径，不碰它的模板缓存，
/// 避免跨测试类污染那个共享实例。
/// </remarks>
public class DefaultTemplateEngineExtensionsTests
{
    /// <summary>
    /// 用变量字典渲染字符串模板
    /// </summary>
    [Fact]
    public void RenderTemplate_WithVariables_ReplacesPlaceholders()
    {
        var variables = new Dictionary<string, object?> { ["name"] = "曦寒" };

        Assert.Equal("你好 曦寒", "你好 {{name}}".RenderTemplate(variables));
    }

    /// <summary>
    /// 用对象模型渲染字符串模板
    /// </summary>
    [Fact]
    public void RenderTemplate_WithModel_ReplacesByPropertyName()
    {
        Assert.Equal("你好 曦寒", "你好 {{Name}}".RenderTemplate(new ExtensionModel { Name = "曦寒" }));
    }

    /// <summary>
    /// 用模板上下文渲染字符串模板
    /// </summary>
    [Fact]
    public void RenderTemplate_WithContext_ReplacesPlaceholders()
    {
        var context = new TemplateContext();
        context.SetVariable("name", "曦寒");

        Assert.Equal("你好 曦寒", "你好 {{name}}".RenderTemplate(context));
    }

    /// <summary>
    /// 从字典一侧发起渲染
    /// </summary>
    [Fact]
    public void RenderTemplate_FromDictionarySide_ReplacesPlaceholders()
    {
        var variables = new Dictionary<string, object?> { ["name"] = "曦寒" };

        Assert.Equal("你好 曦寒", variables.RenderTemplate("你好 {{name}}"));
    }

    /// <summary>
    /// 从对象一侧发起渲染
    /// </summary>
    [Fact]
    public void RenderTemplate_FromModelSide_ReplacesPlaceholders()
    {
        var model = new ExtensionModel { Name = "曦寒" };

        Assert.Equal("你好 曦寒", model.RenderTemplate("你好 {{Name}}"));
    }

    /// <summary>
    /// 校验合法模板返回成功
    /// </summary>
    [Fact]
    public void ValidateTemplate_WhenValid_ReturnsSuccess()
    {
        var result = "你好 {{name}}".ValidateTemplate();

        Assert.True(result.IsValid);
        Assert.True("你好 {{name}}".IsValidTemplate());
    }

    /// <summary>
    /// 校验非法模板返回失败
    /// </summary>
    [Fact]
    public void ValidateTemplate_WhenInvalid_ReturnsFailure()
    {
        var result = "{{if isVip}}会员".ValidateTemplate();

        Assert.False(result.IsValid);
        Assert.False("{{if isVip}}会员".IsValidTemplate());
    }

    /// <summary>
    /// 字典可以转换为模板上下文
    /// </summary>
    [Fact]
    public void ToTemplateContext_FromDictionary_CopiesEntries()
    {
        var variables = new Dictionary<string, object?> { ["name"] = "曦寒", ["age"] = 3 };

        var context = variables.ToTemplateContext();

        Assert.Equal("曦寒", context.GetVariable("name"));
        Assert.Equal(3, context.GetVariable("age"));
    }

    /// <summary>
    /// 对象可以转换为模板上下文
    /// </summary>
    [Fact]
    public void ToTemplateContext_FromModel_CopiesProperties()
    {
        var context = new ExtensionModel { Name = "曦寒" }.ToTemplateContext();

        Assert.Equal("曦寒", context.GetVariable("Name"));
    }

    /// <summary>
    /// 从字符串创建流式构建器
    /// </summary>
    [Fact]
    public void CreateBuilder_ReturnsBuilderBoundToTemplate()
    {
        var builder = "你好 {{name}}".CreateBuilder();

        Assert.Equal("你好 曦寒", builder.WithVariable("name", "曦寒").Render());
    }

    /// <summary>
    /// 扩展方法测试用的模型对象
    /// </summary>
    public sealed class ExtensionModel
    {
        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
