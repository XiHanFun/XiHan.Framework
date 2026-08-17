// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Docs.Mcp.Indexing;

namespace XiHan.Framework.Docs.Mcp.Tests;

/// <summary>
/// 分词器测试
/// </summary>
public class TokenizerTests
{
    /// <summary>
    /// 连续中文按双字切分
    /// </summary>
    [Fact]
    public void 中文切成双字词()
    {
        var terms = Tokenizer.Tokenize("分布式事件");

        Assert.Equal(["分布", "布式", "式事", "事件"], terms);
    }

    /// <summary>
    /// 单个中文字符不产生词条，避免噪声
    /// </summary>
    [Fact]
    public void 单字中文被丢弃()
    {
        var terms = Tokenizer.Tokenize("包");

        Assert.Empty(terms);
    }

    /// <summary>
    /// 英文标识符既保留整词又按 PascalCase 拆词
    /// </summary>
    [Fact]
    public void 帕斯卡命名同时保留整词与拆词()
    {
        var terms = Tokenizer.Tokenize("ILocalEventBus");

        Assert.Contains("ilocaleventbus", terms);
        Assert.Contains("local", terms);
        Assert.Contains("event", terms);
        Assert.Contains("bus", terms);
    }

    /// <summary>
    /// 单字符英文词条被丢弃（如 ILocalEventBus 拆出的 I）
    /// </summary>
    [Fact]
    public void 单字符英文被丢弃()
    {
        var terms = Tokenizer.Tokenize("ILocalEventBus");

        Assert.DoesNotContain("i", terms);
    }

    /// <summary>
    /// 中英混排时两套规则各自生效
    /// </summary>
    [Fact]
    public void 中英混排各自切分()
    {
        var terms = Tokenizer.Tokenize("使用 EventBus 发布");

        Assert.Contains("使用", terms);
        Assert.Contains("eventbus", terms);
        Assert.Contains("发布", terms);
    }

    /// <summary>
    /// 空输入返回空集合而非抛出
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void 空输入返回空集合(string? input)
    {
        Assert.Empty(Tokenizer.Tokenize(input));
    }
}
