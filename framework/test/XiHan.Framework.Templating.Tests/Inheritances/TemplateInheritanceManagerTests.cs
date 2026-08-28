// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;
using XiHan.Framework.Templating.Inheritances;

namespace XiHan.Framework.Templating.Tests.Inheritances;

/// <summary>
/// <see cref="TemplateInheritanceManager"/> 布局注册、继承解析与模板合并的测试
/// </summary>
/// <remarks>
/// 合并使用的块占位符是带裁剪标记的固定形态 <c>{{- block 名称 -}} ... {{- endblock -}}</c>，
/// 与解析块定义时接受的宽松形态并不相同，这条不对称是最容易踩坑的地方，单独锁死。
/// 另外必须证明「布局自我继承」不会把渲染拖进无限递归。
/// </remarks>
public class TemplateInheritanceManagerTests
{
    /// <summary>
    /// 注册后可以取回布局
    /// </summary>
    [Fact]
    public void RegisterLayout_ThenGetLayout_ReturnsTemplate()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        manager.RegisterLayout("base", "布局内容");

        Assert.Equal("布局内容", manager.GetLayout("base"));
    }

    /// <summary>
    /// 取不存在的布局返回 null
    /// </summary>
    [Fact]
    public void GetLayout_WhenMissing_ReturnsNull()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        Assert.Null(manager.GetLayout("missing"));
    }

    /// <summary>
    /// 同名布局重复注册时后注册的生效
    /// </summary>
    [Fact]
    public void RegisterLayout_SameName_Overwrites()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        manager.RegisterLayout("base", "旧布局");
        manager.RegisterLayout("base", "新布局");

        Assert.Equal("新布局", manager.GetLayout("base"));
    }

    /// <summary>
    /// 没有 extends 指令时判定为无继承
    /// </summary>
    [Fact]
    public void ParseInheritance_WhenNoExtends_ReportsNoInheritance()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        var info = manager.ParseInheritance("普通模板 {{name}}");

        Assert.False(info.HasInheritance);
        Assert.Null(info.ParentLayout);
        Assert.Empty(info.Blocks);
    }

    /// <summary>
    /// 解析 extends 指令得到父布局名
    /// </summary>
    /// <param name="templateSource">模板源码</param>
    [Theory]
    [InlineData("{{ extends \"base\" }}")]
    [InlineData("{{extends 'base'}}")]
    [InlineData("{{- extends \"base\" -}}")]
    [InlineData("{{ EXTENDS \"base\" }}")]
    public void ParseInheritance_WithExtends_ReadsParentLayout(string templateSource)
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        var info = manager.ParseInheritance(templateSource);

        Assert.True(info.HasInheritance);
        Assert.Equal("base", info.ParentLayout);
    }

    /// <summary>
    /// 解析块定义得到块名、块内容与位置
    /// </summary>
    [Fact]
    public void ParseInheritance_WithBlocks_ReadsNameContentAndLocation()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        var info = manager.ParseInheritance("第一行\n{{ block content }}块内容{{ endblock }}");

        Assert.True(info.Blocks.ContainsKey("content"));
        var block = info.Blocks["content"];
        Assert.Equal("content", block.Name);
        Assert.Equal("块内容", block.Content);
        Assert.True(block.IsOverridable);
        Assert.NotNull(block.Location);
        // 块起始于第二行第一列，行列号从 1 开始计
        Assert.Equal(2, block.Location.Line);
        Assert.Equal(1, block.Location.Column);
    }

    /// <summary>
    /// 多个块定义各自成条目
    /// </summary>
    [Fact]
    public void ParseInheritance_WithMultipleBlocks_ReadsAll()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        var info = manager.ParseInheritance("{{ block head }}甲{{ endblock }}{{ block body }}乙{{ endblock }}");

        Assert.Equal(2, info.Blocks.Count);
        Assert.Equal("甲", info.Blocks["head"].Content);
        Assert.Equal("乙", info.Blocks["body"].Content);
    }

    /// <summary>
    /// 同名块重复定义时后定义的生效
    /// </summary>
    [Fact]
    public void ParseInheritance_WithDuplicateBlockName_LastWins()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        var info = manager.ParseInheritance("{{ block body }}甲{{ endblock }}{{ block body }}乙{{ endblock }}");

        Assert.Single(info.Blocks);
        Assert.Equal("乙", info.Blocks["body"].Content);
    }

    /// <summary>
    /// 块内容跨行时完整保留
    /// </summary>
    [Fact]
    public void ParseInheritance_WithMultilineBlock_KeepsWholeContent()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        var info = manager.ParseInheritance("{{ block body }}第一行\n第二行{{ endblock }}");

        Assert.Equal("第一行\n第二行", info.Blocks["body"].Content);
    }

    /// <summary>
    /// 合并时用子模板的块内容替换布局里的整段块
    /// </summary>
    [Fact]
    public void MergeTemplate_ReplacesWholeBlockRegion()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());
        const string layout = "头{{- block content -}}默认内容{{- endblock -}}尾";
        var blocks = new Dictionary<string, string> { ["content"] = "子内容" };

        var merged = manager.MergeTemplate("子模板", layout, blocks);

        Assert.Equal("头子内容尾", merged);
    }

    /// <summary>
    /// 布局里没有对应块时原样返回布局
    /// </summary>
    [Fact]
    public void MergeTemplate_WhenBlockNotInLayout_ReturnsLayoutUnchanged()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());
        const string layout = "头{{- block content -}}默认内容{{- endblock -}}尾";
        var blocks = new Dictionary<string, string> { ["notExists"] = "子内容" };

        Assert.Equal(layout, manager.MergeTemplate("子模板", layout, blocks));
    }

    /// <summary>
    /// 没有块要替换时原样返回布局
    /// </summary>
    [Fact]
    public void MergeTemplate_WithEmptyBlocks_ReturnsLayoutUnchanged()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());
        const string layout = "头{{- block content -}}默认内容{{- endblock -}}尾";

        Assert.Equal(layout, manager.MergeTemplate("子模板", layout, new Dictionary<string, string>()));
    }

    /// <summary>
    /// 多个块可以在一次合并中全部替换
    /// </summary>
    [Fact]
    public void MergeTemplate_WithMultipleBlocks_ReplacesEach()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());
        const string layout = "[{{- block head -}}甲{{- endblock -}}]({{- block body -}}乙{{- endblock -}})";
        var blocks = new Dictionary<string, string> { ["head"] = "新甲", ["body"] = "新乙" };

        Assert.Equal("[新甲](新乙)", manager.MergeTemplate("子模板", layout, blocks));
    }

    /// <summary>
    /// 无继承的模板直接用默认引擎渲染
    /// </summary>
    [Fact]
    public async Task RenderInheritedTemplateAsync_WhenNoInheritance_RendersDirectly()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());
        var context = new TemplateContext();
        context.SetVariable("name", "曦寒");

        var output = await manager.RenderInheritedTemplateAsync("你好 {{name}}", context);

        Assert.Equal("你好 曦寒", output);
    }

    /// <summary>
    /// 有继承时先合并布局再渲染
    /// </summary>
    [Fact]
    public async Task RenderInheritedTemplateAsync_WhenInherits_MergesLayoutThenRenders()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());
        manager.RegisterLayout("base", "头{{- block content -}}默认内容{{- endblock -}}尾");
        var context = new TemplateContext();
        context.SetVariable("name", "曦寒");

        var output = await manager.RenderInheritedTemplateAsync(
            "{{ extends \"base\" }}{{ block content }}你好 {{name}}{{ endblock }}",
            context);

        Assert.Equal("头你好 曦寒尾", output);
    }

    /// <summary>
    /// 声明继承但布局未注册时抛出无效操作异常
    /// </summary>
    [Fact]
    public async Task RenderInheritedTemplateAsync_WhenLayoutMissing_Throws()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.RenderInheritedTemplateAsync("{{ extends \"missing\" }}", new TemplateContext()));

        Assert.Contains("找不到布局模板", exception.Message);
    }

    /// <summary>
    /// 注册表里没有可用引擎时渲染抛出无效操作异常
    /// </summary>
    [Fact]
    public async Task RenderInheritedTemplateAsync_WhenNoEngineRegistered_Throws()
    {
        var manager = new TemplateInheritanceManager(new TemplateEngineRegistry());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.RenderInheritedTemplateAsync("普通模板", new TemplateContext()));

        Assert.Contains("模板引擎", exception.Message);
    }

    /// <summary>
    /// 布局继承自身时渲染仍然在有限步内结束
    /// </summary>
    [Fact]
    public async Task RenderInheritedTemplateAsync_WhenLayoutExtendsItself_TerminatesWithoutInfiniteRecursion()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());
        // 布局自己又声明继承自己，构成最短的一条循环继承链
        manager.RegisterLayout("loop", "{{ extends \"loop\" }}布局体");

        var renderTask = manager.RenderInheritedTemplateAsync("{{ extends \"loop\" }}子模板", new TemplateContext());
        var finished = await Task.WhenAny(renderTask, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.True(ReferenceEquals(finished, renderTask), "循环继承必须在有限步内结束，不能把渲染拖死");
        Assert.Contains("布局体", await renderTask);
    }

    /// <summary>
    /// 两个布局互相继承时渲染仍然在有限步内结束
    /// </summary>
    [Fact]
    public async Task RenderInheritedTemplateAsync_WhenLayoutsExtendEachOther_Terminates()
    {
        var manager = new TemplateInheritanceManager(CreateRegistry());
        manager.RegisterLayout("first", "{{ extends \"second\" }}第一个布局");
        manager.RegisterLayout("second", "{{ extends \"first\" }}第二个布局");

        var renderTask = manager.RenderInheritedTemplateAsync("{{ extends \"first\" }}子模板", new TemplateContext());
        var finished = await Task.WhenAny(renderTask, Task.Delay(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Assert.True(ReferenceEquals(finished, renderTask), "互相继承的两个布局必须在有限步内结束");
        Assert.Contains("第一个布局", await renderTask);
    }

    /// <summary>
    /// 创建一个已注册 string 默认引擎的注册表
    /// </summary>
    /// <returns>模板引擎注册表</returns>
    private static ITemplateEngineRegistry CreateRegistry()
    {
        var registry = new TemplateEngineRegistry();
        registry.RegisterEngine("String", new DefaultTemplateEngine());
        registry.SetDefaultEngine<string>("String");
        return registry;
    }
}
