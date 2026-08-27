// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Templating.Inheritances;

namespace XiHan.Framework.Templating.Tests.Inheritances;

/// <summary>
/// <see cref="MemoryPartialProvider"/> 片段增删改查与变更监听的测试
/// </summary>
/// <remarks>
/// 内存提供者是片段注册表里优先级最低（0）的兜底来源，且明确不支持变更监听——
/// WatchChanges 返回 null 是契约的一部分，调用方必须按可空处理。
/// </remarks>
public class MemoryPartialProviderTests
{
    /// <summary>
    /// 提供者名称与优先级是固定值
    /// </summary>
    [Fact]
    public void NameAndPriority_AreStable()
    {
        var provider = new MemoryPartialProvider();

        Assert.Equal("Memory", provider.Name);
        Assert.Equal(0, provider.Priority);
    }

    /// <summary>
    /// 未添加任何片段时不支持任何名称
    /// </summary>
    [Fact]
    public void SupportsPartial_WhenEmpty_ReturnsFalse()
    {
        var provider = new MemoryPartialProvider();

        Assert.False(provider.SupportsPartial("header"));
    }

    /// <summary>
    /// 添加片段后可以查到并取回内容
    /// </summary>
    [Fact]
    public async Task AddPartial_ThenGetPartialTemplate_ReturnsContent()
    {
        var provider = new MemoryPartialProvider();

        provider.AddPartial("header", "<h1>{{title}}</h1>");

        Assert.True(provider.SupportsPartial("header"));
        Assert.Equal("<h1>{{title}}</h1>", await provider.GetPartialTemplateAsync("header"));
    }

    /// <summary>
    /// 取不存在的片段返回 null
    /// </summary>
    [Fact]
    public async Task GetPartialTemplateAsync_WhenMissing_ReturnsNull()
    {
        var provider = new MemoryPartialProvider();

        Assert.Null(await provider.GetPartialTemplateAsync("missing"));
    }

    /// <summary>
    /// 更新片段覆盖原内容
    /// </summary>
    [Fact]
    public async Task UpdatePartial_OverwritesExistingContent()
    {
        var provider = new MemoryPartialProvider();
        provider.AddPartial("header", "旧内容");

        provider.UpdatePartial("header", "新内容");

        Assert.Equal("新内容", await provider.GetPartialTemplateAsync("header"));
    }

    /// <summary>
    /// 更新不存在的片段等同于新增
    /// </summary>
    [Fact]
    public async Task UpdatePartial_WhenMissing_AddsIt()
    {
        var provider = new MemoryPartialProvider();

        provider.UpdatePartial("header", "内容");

        Assert.Equal("内容", await provider.GetPartialTemplateAsync("header"));
    }

    /// <summary>
    /// 添加同名片段时后添加的覆盖先添加的
    /// </summary>
    [Fact]
    public async Task AddPartial_SameName_LastWins()
    {
        var provider = new MemoryPartialProvider();

        provider.AddPartial("header", "第一次");
        provider.AddPartial("header", "第二次");

        Assert.Equal("第二次", await provider.GetPartialTemplateAsync("header"));
    }

    /// <summary>
    /// 移除片段返回是否命中
    /// </summary>
    [Fact]
    public void RemovePartial_ReturnsWhetherRemoved()
    {
        var provider = new MemoryPartialProvider();
        provider.AddPartial("header", "内容");

        Assert.True(provider.RemovePartial("header"));
        Assert.False(provider.RemovePartial("header"));
        Assert.False(provider.SupportsPartial("header"));
    }

    /// <summary>
    /// 清空后所有片段都取不到
    /// </summary>
    [Fact]
    public void ClearPartials_RemovesEverything()
    {
        var provider = new MemoryPartialProvider();
        provider.AddPartial("header", "内容甲");
        provider.AddPartial("footer", "内容乙");

        provider.ClearPartials();

        Assert.False(provider.SupportsPartial("header"));
        Assert.False(provider.SupportsPartial("footer"));
    }

    /// <summary>
    /// 片段信息包含名称与长度，且内存来源没有路径
    /// </summary>
    [Fact]
    public async Task GetPartialInfoAsync_WhenPresent_DescribesPartial()
    {
        var provider = new MemoryPartialProvider();
        const string template = "<h1>{{title}}</h1>";
        provider.AddPartial("header", template);

        var info = await provider.GetPartialInfoAsync("header");

        Assert.NotNull(info);
        Assert.Equal("header", info.Name);
        // 内存片段没有落盘，路径必须为空，否则调用方会误以为能按路径重新加载
        Assert.Null(info.Path);
        Assert.Equal(template.Length, info.Size);
        Assert.False(string.IsNullOrEmpty(info.ContentHash));
        Assert.Empty(info.Dependencies);
        Assert.Empty(info.Metadata);
    }

    /// <summary>
    /// 取不存在片段的信息返回 null
    /// </summary>
    [Fact]
    public async Task GetPartialInfoAsync_WhenMissing_ReturnsNull()
    {
        var provider = new MemoryPartialProvider();

        Assert.Null(await provider.GetPartialInfoAsync("missing"));
    }

    /// <summary>
    /// 内存提供者不支持变更监听，返回空监听器
    /// </summary>
    [Fact]
    public void WatchChanges_ReturnsNull()
    {
        var provider = new MemoryPartialProvider();

        Assert.Null(provider.WatchChanges(_ => { }));
    }

    /// <summary>
    /// 并发添加不同片段不会丢失
    /// </summary>
    [Fact]
    public void AddPartial_FromMultipleThreads_KeepsAll()
    {
        var provider = new MemoryPartialProvider();
        const int count = 100;

        Parallel.For(0, count, index => provider.AddPartial($"partial{index}", $"内容{index}"));

        for (var index = 0; index < count; index++)
        {
            Assert.True(provider.SupportsPartial($"partial{index}"));
        }
    }
}
