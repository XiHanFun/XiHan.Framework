// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Tests.Engines;

/// <summary>
/// <see cref="TemplateBuilder"/> 流式变量装配与渲染的测试
/// </summary>
/// <remarks>
/// 构建器只能通过字符串扩展方法 CreateBuilder 获得（构造函数是 internal），
/// 因此测试也从这个唯一入口进入，顺带锁死 Clone 的浅拷贝独立性。
/// </remarks>
public class TemplateBuilderTests
{
    /// <summary>
    /// 逐个添加变量后渲染
    /// </summary>
    [Fact]
    public void WithVariable_ThenRender_ReplacesPlaceholders()
    {
        var result = "{{greeting}} {{name}}"
            .CreateBuilder()
            .WithVariable("greeting", "你好")
            .WithVariable("name", "曦寒")
            .Render();

        Assert.Equal("你好 曦寒", result);
    }

    /// <summary>
    /// 批量添加变量
    /// </summary>
    [Fact]
    public void WithVariables_MergesDictionary()
    {
        var builder = "{{a}}{{b}}"
            .CreateBuilder()
            .WithVariables(new Dictionary<string, object?> { ["a"] = "甲", ["b"] = "乙" });

        Assert.Equal("甲乙", builder.Render());
    }

    /// <summary>
    /// 添加对象模型时按属性名展开
    /// </summary>
    [Fact]
    public void WithModel_UsesPropertyNames()
    {
        var result = "你好 {{Name}}"
            .CreateBuilder()
            .WithModel(new BuilderModel { Name = "曦寒" })
            .Render();

        Assert.Equal("你好 曦寒", result);
    }

    /// <summary>
    /// 指定前缀时变量名带上前缀
    /// </summary>
    [Fact]
    public void WithModel_WithPrefix_PrefixesVariableNames()
    {
        var builder = "你好 {{user.Name}}"
            .CreateBuilder()
            .WithModel(new BuilderModel { Name = "曦寒" }, "user");

        Assert.Equal("你好 曦寒", builder.Render());
        Assert.True(builder.GetVariables().ContainsKey("user.Name"));
    }

    /// <summary>
    /// 条件为真时才添加变量
    /// </summary>
    [Fact]
    public void WithVariableIf_WhenConditionTrue_AddsVariable()
    {
        var builder = "{{name}}".CreateBuilder().WithVariableIf(true, "name", "曦寒");

        Assert.Equal("曦寒", builder.Render());
    }

    /// <summary>
    /// 条件为假时不添加变量，占位符原样保留
    /// </summary>
    [Fact]
    public void WithVariableIf_WhenConditionFalse_SkipsVariable()
    {
        var builder = "{{name}}".CreateBuilder().WithVariableIf(false, "name", "曦寒");

        Assert.Empty(builder.GetVariables());
        // 没有任何变量时默认引擎直接返回原模板
        Assert.Equal("{{name}}", builder.Render());
    }

    /// <summary>
    /// 条件函数为真时才添加变量
    /// </summary>
    [Fact]
    public void WithVariableIf_WithPredicate_EvaluatesPredicate()
    {
        var evaluated = 0;

        var builder = "{{name}}".CreateBuilder().WithVariableIf(() =>
        {
            evaluated++;
            return true;
        }, "name", "曦寒");

        Assert.Equal(1, evaluated);
        Assert.Equal("曦寒", builder.Render());
    }

    /// <summary>
    /// 获取变量返回的是副本，改动不回写构建器
    /// </summary>
    [Fact]
    public void GetVariables_ReturnsSnapshotCopy()
    {
        var builder = "{{name}}".CreateBuilder().WithVariable("name", "曦寒");

        var snapshot = builder.GetVariables();
        snapshot["name"] = "被改了";

        Assert.Equal("曦寒", builder.Render());
    }

    /// <summary>
    /// 清空变量后渲染回到原模板
    /// </summary>
    [Fact]
    public void Clear_RemovesAllVariables()
    {
        var builder = "{{name}}".CreateBuilder().WithVariable("name", "曦寒");

        Assert.Same(builder, builder.Clear());
        Assert.Empty(builder.GetVariables());
    }

    /// <summary>
    /// 克隆后两个构建器互不影响
    /// </summary>
    [Fact]
    public void Clone_ProducesIndependentBuilder()
    {
        var original = "{{name}}".CreateBuilder().WithVariable("name", "原始");

        var cloned = original.Clone().WithVariable("name", "克隆");

        Assert.NotSame(original, cloned);
        Assert.Equal("原始", original.Render());
        Assert.Equal("克隆", cloned.Render());
    }

    /// <summary>
    /// 异步渲染与同步渲染结果一致
    /// </summary>
    [Fact]
    public async Task RenderAsync_MatchesSyncRender()
    {
        var builder = "你好 {{name}}".CreateBuilder().WithVariable("name", "曦寒");

        Assert.Equal(builder.Render(), await builder.RenderAsync());
    }

    /// <summary>
    /// 构建器可以直接校验其模板
    /// </summary>
    [Fact]
    public void Validate_ChecksOwnTemplate()
    {
        Assert.True("你好 {{name}}".CreateBuilder().Validate().IsValid);
        Assert.False("{{if isVip}}会员".CreateBuilder().Validate().IsValid);
    }

    /// <summary>
    /// 构建器测试用的模型对象
    /// </summary>
    public sealed class BuilderModel
    {
        /// <summary>
        /// 姓名
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
