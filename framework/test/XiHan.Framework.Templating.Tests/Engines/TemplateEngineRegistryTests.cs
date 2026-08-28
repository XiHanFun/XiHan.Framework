// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Scriban;
using XiHan.Framework.Templating.Engines;

namespace XiHan.Framework.Templating.Tests.Engines;

/// <summary>
/// <see cref="TemplateEngineRegistry"/> 引擎注册、查找与默认引擎选择的测试
/// </summary>
/// <remarks>
/// 注册表用「模板类型 + 引擎名」两段键隔离不同模板类型，因此同名引擎在不同模板类型下互不干扰。
/// 默认引擎有两级回退：显式设置优先，未设置时取该模板类型下第一个已注册引擎，全空才返回 null。
/// </remarks>
public class TemplateEngineRegistryTests
{
    /// <summary>
    /// 新建的注册表是空的
    /// </summary>
    [Fact]
    public void Count_WhenNew_IsZero()
    {
        var registry = new TemplateEngineRegistry();

        Assert.Equal(0, registry.Count);
        Assert.Null(registry.GetEngine<string>("String"));
        Assert.Null(registry.GetDefaultEngine<string>());
    }

    /// <summary>
    /// 注册后可以按名取回同一个引擎实例
    /// </summary>
    [Fact]
    public void RegisterEngine_ThenGetEngine_ReturnsSameInstance()
    {
        var registry = new TemplateEngineRegistry();
        var engine = new DefaultTemplateEngine();

        registry.RegisterEngine("String", engine);

        Assert.Same(engine, registry.GetEngine<string>("String"));
        Assert.True(registry.ContainsEngine<string>("String"));
        Assert.Equal(1, registry.Count);
    }

    /// <summary>
    /// 同名引擎重复注册时后注册的覆盖先注册的
    /// </summary>
    [Fact]
    public void RegisterEngine_SameName_Overwrites()
    {
        var registry = new TemplateEngineRegistry();
        var first = new DefaultTemplateEngine();
        var second = new DefaultTemplateEngine();

        registry.RegisterEngine("String", first);
        registry.RegisterEngine("String", second);

        Assert.Same(second, registry.GetEngine<string>("String"));
        Assert.Equal(1, registry.Count);
    }

    /// <summary>
    /// 引擎名相同但模板类型不同的两个引擎互不干扰
    /// </summary>
    [Fact]
    public void RegisterEngine_SameNameDifferentTemplateType_AreIsolated()
    {
        var registry = new TemplateEngineRegistry();
        var stringEngine = new DefaultTemplateEngine();
        var scribanEngine = new ScribanTemplateEngine();

        registry.RegisterEngine("Shared", stringEngine);
        registry.RegisterEngine("Shared", scribanEngine);

        Assert.Same(stringEngine, registry.GetEngine<string>("Shared"));
        Assert.Same(scribanEngine, registry.GetEngine<Template>("Shared"));
        Assert.Equal(2, registry.Count);
    }

    /// <summary>
    /// 查询不存在的引擎名返回 null
    /// </summary>
    [Fact]
    public void GetEngine_WhenNameNotRegistered_ReturnsNull()
    {
        var registry = new TemplateEngineRegistry();
        registry.RegisterEngine("String", new DefaultTemplateEngine());

        Assert.Null(registry.GetEngine<string>("NotExists"));
        Assert.False(registry.ContainsEngine<string>("NotExists"));
    }

    /// <summary>
    /// 未显式设置默认引擎时回退到该模板类型下已注册的引擎
    /// </summary>
    [Fact]
    public void GetDefaultEngine_WhenNotConfigured_FallsBackToRegisteredEngine()
    {
        var registry = new TemplateEngineRegistry();
        var engine = new DefaultTemplateEngine();
        registry.RegisterEngine("String", engine);

        Assert.Same(engine, registry.GetDefaultEngine<string>());
    }

    /// <summary>
    /// 回退查找不会跨模板类型串用引擎
    /// </summary>
    [Fact]
    public void GetDefaultEngine_FallbackDoesNotCrossTemplateType()
    {
        var registry = new TemplateEngineRegistry();
        registry.RegisterEngine("Scriban", new ScribanTemplateEngine());

        // 只注册了 Template 类型的引擎，string 类型必须仍然取不到，否则会在渲染时炸出类型转换异常
        Assert.Null(registry.GetDefaultEngine<string>());
        Assert.NotNull(registry.GetDefaultEngine<Template>());
    }

    /// <summary>
    /// 显式设置的默认引擎优先于回退
    /// </summary>
    [Fact]
    public void SetDefaultEngine_TakesPrecedenceOverFallback()
    {
        var registry = new TemplateEngineRegistry();
        var first = new DefaultTemplateEngine();
        var second = new DefaultTemplateEngine();
        registry.RegisterEngine("First", first);
        registry.RegisterEngine("Second", second);

        registry.SetDefaultEngine<string>("Second");

        Assert.Same(second, registry.GetDefaultEngine<string>());
    }

    /// <summary>
    /// 默认引擎指向未注册的名字时返回 null
    /// </summary>
    [Fact]
    public void GetDefaultEngine_WhenDefaultNameNotRegistered_ReturnsNull()
    {
        var registry = new TemplateEngineRegistry();
        registry.RegisterEngine("String", new DefaultTemplateEngine());

        registry.SetDefaultEngine<string>("NotExists");

        // 显式设置一旦生效就不再回退，指错名字必须暴露成 null 而不是悄悄换一个引擎
        Assert.Null(registry.GetDefaultEngine<string>());
    }

    /// <summary>
    /// 移除引擎返回是否命中
    /// </summary>
    [Fact]
    public void RemoveEngine_ReturnsWhetherRemoved()
    {
        var registry = new TemplateEngineRegistry();
        registry.RegisterEngine("String", new DefaultTemplateEngine());

        Assert.True(registry.RemoveEngine<string>("String"));
        Assert.False(registry.RemoveEngine<string>("String"));
        Assert.Equal(0, registry.Count);
    }

    /// <summary>
    /// 移除的正是默认引擎时同时清掉默认设置
    /// </summary>
    [Fact]
    public void RemoveEngine_WhenRemovingDefault_ClearsDefaultSetting()
    {
        var registry = new TemplateEngineRegistry();
        var first = new DefaultTemplateEngine();
        var second = new DefaultTemplateEngine();
        registry.RegisterEngine("First", first);
        registry.RegisterEngine("Second", second);
        registry.SetDefaultEngine<string>("Second");

        registry.RemoveEngine<string>("Second");

        // 默认设置被清掉后重新走回退，取到剩下的那个引擎
        Assert.Same(first, registry.GetDefaultEngine<string>());
    }

    /// <summary>
    /// 移除非默认引擎不影响默认设置
    /// </summary>
    [Fact]
    public void RemoveEngine_WhenRemovingOther_KeepsDefaultSetting()
    {
        var registry = new TemplateEngineRegistry();
        var first = new DefaultTemplateEngine();
        var second = new DefaultTemplateEngine();
        registry.RegisterEngine("First", first);
        registry.RegisterEngine("Second", second);
        registry.SetDefaultEngine<string>("Second");

        registry.RemoveEngine<string>("First");

        Assert.Same(second, registry.GetDefaultEngine<string>());
    }

    /// <summary>
    /// 引擎名集合只返回对应模板类型的引擎名
    /// </summary>
    [Fact]
    public void GetEngineNames_ReturnsNamesOfMatchingTemplateTypeOnly()
    {
        var registry = new TemplateEngineRegistry();
        registry.RegisterEngine("String", new DefaultTemplateEngine());
        registry.RegisterEngine("Another", new DefaultTemplateEngine());
        registry.RegisterEngine("Scriban", new ScribanTemplateEngine());

        var names = registry.GetEngineNames<string>().ToList();

        Assert.Equal(2, names.Count);
        Assert.Contains("String", names);
        Assert.Contains("Another", names);
        Assert.DoesNotContain("Scriban", names);
    }

    /// <summary>
    /// 清空后引擎与默认设置一起消失
    /// </summary>
    [Fact]
    public void Clear_RemovesEnginesAndDefaults()
    {
        var registry = new TemplateEngineRegistry();
        registry.RegisterEngine("String", new DefaultTemplateEngine());
        registry.SetDefaultEngine<string>("String");

        registry.Clear();

        Assert.Equal(0, registry.Count);
        Assert.Null(registry.GetDefaultEngine<string>());
        Assert.Empty(registry.GetEngineNames<string>());
    }

    /// <summary>
    /// 并发注册不同名字的引擎不会丢失
    /// </summary>
    [Fact]
    public void RegisterEngine_FromMultipleThreads_KeepsAllEngines()
    {
        var registry = new TemplateEngineRegistry();
        const int count = 100;

        Parallel.For(0, count, index => registry.RegisterEngine($"engine{index}", new DefaultTemplateEngine()));

        Assert.Equal(count, registry.Count);
        Assert.Equal(count, registry.GetEngineNames<string>().Count());
    }
}
