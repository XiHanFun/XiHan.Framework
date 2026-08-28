// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.Sqids;

namespace XiHan.Framework.DistributedIds.Tests.Sqids;

/// <summary>
/// Sqids 配置选项的测试
/// </summary>
/// <remarks>
/// 默认字母表与最小长度决定了对外暴露的短 ID 形态，改动会让历史短 ID 解不回来，因此逐个锁死；
/// 屏蔽词表必须按大小写不敏感匹配，否则「换个大小写就绕过」等于没有屏蔽。
/// </remarks>
public class SqidsOptionsTests
{
    /// <summary>
    /// 配置节名称被 appsettings 直接引用，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:DistributedIds:Sqids", SqidsOptions.SectionName);
    }

    /// <summary>
    /// 默认字母表是 62 个互不重复的字母数字字符
    /// </summary>
    [Fact]
    public void DefaultAlphabet_IsStable()
    {
        var options = new SqidsOptions();

        Assert.Equal("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", options.Alphabet);
        Assert.Equal(62, options.Alphabet.Length);
        Assert.Equal(options.Alphabet.Length, options.Alphabet.Distinct().Count());
    }

    /// <summary>
    /// 默认最小长度为 5
    /// </summary>
    [Fact]
    public void DefaultMinLength_IsFive()
    {
        Assert.Equal(5, new SqidsOptions().MinLength);
    }

    /// <summary>
    /// 默认屏蔽词表非空且按大小写不敏感匹配
    /// </summary>
    [Fact]
    public void DefaultBlockList_IsCaseInsensitiveAndNotEmpty()
    {
        var options = new SqidsOptions();

        Assert.NotEmpty(options.BlockList);
        // 直接走集合自身的比较器，才能验证 OrdinalIgnoreCase 真的生效
        Assert.Contains("fuck", options.BlockList);
        Assert.Contains("FUCK", options.BlockList);
        Assert.Contains("FuCk", options.BlockList);
    }

    /// <summary>
    /// 每个选项实例持有独立的屏蔽词表副本，改一个不会污染另一个
    /// </summary>
    [Fact]
    public void BlockList_IsIndependentPerInstance()
    {
        var first = new SqidsOptions();
        var second = new SqidsOptions();

        first.BlockList.Clear();

        Assert.Empty(first.BlockList);
        Assert.NotEmpty(second.BlockList);
    }

    /// <summary>
    /// 三个属性都可被配置覆盖
    /// </summary>
    [Fact]
    public void Properties_AreOverridable()
    {
        var options = new SqidsOptions
        {
            Alphabet = "abcdefghijklmnopqrstuvwxyz",
            MinLength = 8,
            BlockList = ["banned"]
        };

        Assert.Equal("abcdefghijklmnopqrstuvwxyz", options.Alphabet);
        Assert.Equal(8, options.MinLength);
        Assert.Single(options.BlockList);
    }
}
