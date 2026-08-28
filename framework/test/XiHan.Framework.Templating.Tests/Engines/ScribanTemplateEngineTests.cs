// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Tests.Engines;

/// <summary>
/// <see cref="ScribanTemplateEngine"/> 解析、校验与渲染的测试
/// </summary>
/// <remarks>
/// 该引擎的公共契约是「把 <see cref="ITemplateContext"/> 里的变量与函数桥接给 Scriban 再渲染」，
/// 因此变量替换用例按这条契约断言，而不是按当前实现的输出反推。
/// </remarks>
public class ScribanTemplateEngineTests
{
    /// <summary>
    /// 合法模板解析后不带错误
    /// </summary>
    [Fact]
    public void Parse_WhenSourceValid_ReturnsTemplateWithoutErrors()
    {
        var engine = new ScribanTemplateEngine();

        var template = engine.Parse("你好 {{ name }}");

        Assert.NotNull(template);
        Assert.False(template.HasErrors);
    }

    /// <summary>
    /// 非法模板解析后带错误
    /// </summary>
    [Fact]
    public void Parse_WhenSourceInvalid_ReturnsTemplateWithErrors()
    {
        var engine = new ScribanTemplateEngine();

        var template = engine.Parse("{{ if true }}缺少结束语句");

        Assert.True(template.HasErrors);
    }

    /// <summary>
    /// 合法模板校验通过
    /// </summary>
    [Fact]
    public void Validate_WhenSourceValid_ReturnsSuccess()
    {
        var engine = new ScribanTemplateEngine();

        var result = engine.Validate("你好 {{ name }}");

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>
    /// 非法模板校验失败并给出错误消息与位置
    /// </summary>
    [Fact]
    public void Validate_WhenSourceInvalid_ReturnsFailureWithPosition()
    {
        var engine = new ScribanTemplateEngine();

        var result = engine.Validate("{{ if true }}缺少结束语句");

        Assert.False(result.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        // 行列号从 Scriban 的零基位置换算成一基，最小合法值是 1
        Assert.NotNull(result.ErrorLine);
        Assert.True(result.ErrorLine >= 1);
        Assert.NotNull(result.ErrorColumn);
        Assert.True(result.ErrorColumn >= 1);
    }

    /// <summary>
    /// 空模板校验通过
    /// </summary>
    [Fact]
    public void Validate_WhenSourceEmpty_ReturnsSuccess()
    {
        var engine = new ScribanTemplateEngine();

        Assert.True(engine.Validate(string.Empty).IsValid);
    }

    /// <summary>
    /// 纯文本模板原样输出
    /// </summary>
    [Fact]
    public void Render_LiteralText_ReturnsTextUnchanged()
    {
        var engine = new ScribanTemplateEngine();

        var output = engine.Render(engine.Parse("纯文本，不含表达式"), new TemplateContext());

        Assert.Equal("纯文本，不含表达式", output);
    }

    /// <summary>
    /// 字面量集合上的循环被逐项展开
    /// </summary>
    [Fact]
    public void Render_WithLiteralLoop_ExpandsEachItem()
    {
        var engine = new ScribanTemplateEngine();

        var output = engine.Render(engine.Parse("{{ for item in [1, 2, 3] }}[{{ item }}]{{ end }}"), new TemplateContext());

        Assert.Equal("[1][2][3]", output);
    }

    /// <summary>
    /// 条件语句按分支输出
    /// </summary>
    [Fact]
    public void Render_WithCondition_SelectsBranch()
    {
        var engine = new ScribanTemplateEngine();

        Assert.Equal("是", engine.Render(engine.Parse("{{ if true }}是{{ else }}否{{ end }}"), new TemplateContext()));
        Assert.Equal("否", engine.Render(engine.Parse("{{ if false }}是{{ else }}否{{ end }}"), new TemplateContext()));
    }

    /// <summary>
    /// 上下文里没有的变量渲染为空字符串而不是抛异常
    /// </summary>
    [Fact]
    public void Render_WhenVariableMissing_RendersEmptyString()
    {
        var engine = new ScribanTemplateEngine();

        var output = engine.Render(engine.Parse("你好 {{ name }}"), new TemplateContext());

        Assert.Equal("你好 ", output);
    }

    /// <summary>
    /// 上下文中的变量被替换到渲染结果里
    /// </summary>
    /// <remarks>
    /// 该用例按引擎宣称的契约断言：<see cref="ITemplateContext"/> 中的变量必须能在模板里取到。
    /// 若此用例失败，说明上下文到 Scriban 的桥接没有真正生效（详见交付报告的「疑似缺陷」）。
    /// </remarks>
    [Fact]
    public void Render_WithContextVariable_SubstitutesValue()
    {
        var engine = new ScribanTemplateEngine();
        var context = new TemplateContext();
        context.SetVariable("name", "曦寒");

        var output = engine.Render(engine.Parse("你好 {{ name }}"), context);

        Assert.Equal("你好 曦寒", output);
    }

    /// <summary>
    /// 异步渲染与同步渲染结果一致
    /// </summary>
    [Fact]
    public async Task RenderAsync_MatchesSyncRender()
    {
        var engine = new ScribanTemplateEngine();
        var context = new TemplateContext();

        var template = engine.Parse("{{ for item in [1, 2] }}[{{ item }}]{{ end }}");
        var asyncOutput = await engine.RenderAsync(template, context, TestContext.Current.CancellationToken);

        Assert.Equal("[1][2]", asyncOutput);
        Assert.Equal(engine.Render(engine.Parse("{{ for item in [1, 2] }}[{{ item }}]{{ end }}"), context), asyncOutput);
    }
}
