// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Contexts;
using XiHan.Framework.Templating.Engines;
using XiHan.Framework.Templating.Inheritances;

namespace XiHan.Framework.Templating.Tests.Inheritances;

/// <summary>
/// <see cref="TemplatePartialManager"/> 片段注册与渲染的测试
/// </summary>
/// <remarks>
/// 渲染片段依赖注册表里 string 模板类型的默认引擎，两条失败路径都必须炸出明确异常：
/// 片段不存在、引擎不存在。静默返回空串会让页面缺块却查不出原因。
/// </remarks>
public class TemplatePartialManagerTests
{
    /// <summary>
    /// 注册后可以同步取回片段
    /// </summary>
    [Fact]
    public void RegisterPartial_ThenGetPartial_ReturnsTemplate()
    {
        var manager = new TemplatePartialManager(CreateRegistry());

        manager.RegisterPartial("header", "<h1>{{title}}</h1>");

        Assert.Equal("<h1>{{title}}</h1>", manager.GetPartial("header"));
    }

    /// <summary>
    /// 取不存在的片段返回 null
    /// </summary>
    [Fact]
    public void GetPartial_WhenMissing_ReturnsNull()
    {
        var manager = new TemplatePartialManager(CreateRegistry());

        Assert.Null(manager.GetPartial("missing"));
    }

    /// <summary>
    /// 异步取片段与同步结果一致
    /// </summary>
    [Fact]
    public async Task GetPartialAsync_MatchesSyncResult()
    {
        var manager = new TemplatePartialManager(CreateRegistry());
        manager.RegisterPartial("header", "内容");

        Assert.Equal("内容", await manager.GetPartialAsync("header"));
        Assert.Null(await manager.GetPartialAsync("missing"));
    }

    /// <summary>
    /// 同名片段重复注册时后注册的生效
    /// </summary>
    [Fact]
    public void RegisterPartial_SameName_Overwrites()
    {
        var manager = new TemplatePartialManager(CreateRegistry());

        manager.RegisterPartial("header", "旧内容");
        manager.RegisterPartial("header", "新内容");

        Assert.Equal("新内容", manager.GetPartial("header"));
        Assert.Single(manager.GetPartialNames());
    }

    /// <summary>
    /// 移除片段返回是否命中
    /// </summary>
    [Fact]
    public void RemovePartial_ReturnsWhetherRemoved()
    {
        var manager = new TemplatePartialManager(CreateRegistry());
        manager.RegisterPartial("header", "内容");

        Assert.True(manager.RemovePartial("header"));
        Assert.False(manager.RemovePartial("header"));
        Assert.Null(manager.GetPartial("header"));
    }

    /// <summary>
    /// 片段名集合包含已注册的全部名称
    /// </summary>
    [Fact]
    public void GetPartialNames_ReturnsAllRegisteredNames()
    {
        var manager = new TemplatePartialManager(CreateRegistry());
        manager.RegisterPartial("header", "甲");
        manager.RegisterPartial("footer", "乙");

        var names = manager.GetPartialNames().ToList();

        Assert.Equal(2, names.Count);
        Assert.Contains("header", names);
        Assert.Contains("footer", names);
    }

    /// <summary>
    /// 清空后片段全部消失
    /// </summary>
    [Fact]
    public void ClearPartialCache_RemovesEverything()
    {
        var manager = new TemplatePartialManager(CreateRegistry());
        manager.RegisterPartial("header", "甲");
        manager.RegisterPartial("footer", "乙");

        manager.ClearPartialCache();

        Assert.Empty(manager.GetPartialNames());
    }

    /// <summary>
    /// 渲染片段时用默认引擎带上下文变量渲染
    /// </summary>
    [Fact]
    public async Task RenderPartialAsync_RendersWithContextVariables()
    {
        var manager = new TemplatePartialManager(CreateRegistry());
        manager.RegisterPartial("header", "<h1>{{title}}</h1>");
        var context = new TemplateContext();
        context.SetVariable("title", "曦寒");

        var output = await manager.RenderPartialAsync("header", context);

        Assert.Equal("<h1>曦寒</h1>", output);
    }

    /// <summary>
    /// 渲染不存在的片段抛出无效操作异常
    /// </summary>
    [Fact]
    public async Task RenderPartialAsync_WhenPartialMissing_Throws()
    {
        var manager = new TemplatePartialManager(CreateRegistry());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.RenderPartialAsync("missing", new TemplateContext()));

        Assert.Contains("找不到模板片段", exception.Message);
    }

    /// <summary>
    /// 注册表里没有可用引擎时渲染抛出无效操作异常
    /// </summary>
    [Fact]
    public async Task RenderPartialAsync_WhenNoEngineRegistered_Throws()
    {
        var manager = new TemplatePartialManager(new TemplateEngineRegistry());
        manager.RegisterPartial("header", "内容");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.RenderPartialAsync("header", new TemplateContext()));

        Assert.Contains("模板引擎", exception.Message);
    }

    /// <summary>
    /// 预编译当前是空实现，调用后立即完成且不改变片段
    /// </summary>
    [Fact]
    public async Task PrecompileAllPartialsAsync_CompletesWithoutSideEffects()
    {
        var manager = new TemplatePartialManager(CreateRegistry());
        manager.RegisterPartial("header", "内容");

        await manager.PrecompileAllPartialsAsync();

        Assert.Equal("内容", manager.GetPartial("header"));
        Assert.Single(manager.GetPartialNames());
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
