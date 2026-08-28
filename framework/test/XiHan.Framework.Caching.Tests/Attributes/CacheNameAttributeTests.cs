// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Caching.Attributes;
using XiHan.Framework.Caching.Tests.Fakes;

namespace XiHan.Framework.Caching.Tests.Attributes;

/// <summary>
/// 缓存名称特性测试
/// </summary>
/// <remarks>
/// 缓存名是规范化键的中间段，直接决定不同缓存项之间的隔离边界。
/// 未标注时按「类型全名去掉 CacheItem 后缀」推导，这个约定一旦变化，所有历史键都会失效，因此逐条锁死。
/// </remarks>
public class CacheNameAttributeTests
{
    /// <summary>
    /// 标注了名称时直接取标注值
    /// </summary>
    [Fact]
    public void GetCacheName_WithAttribute_ReturnsAttributeName()
    {
        Assert.Equal("sample", CacheNameAttribute.GetCacheName(typeof(SampleCacheItem)));
    }

    /// <summary>
    /// 泛型重载与类型重载结果一致
    /// </summary>
    [Fact]
    public void GetCacheName_GenericOverload_MatchesTypeOverload()
    {
        Assert.Equal(
            CacheNameAttribute.GetCacheName(typeof(SampleCacheItem)),
            CacheNameAttribute.GetCacheName<SampleCacheItem>());
    }

    /// <summary>
    /// 未标注名称时按类型全名去掉 CacheItem 后缀推导
    /// </summary>
    [Fact]
    public void GetCacheName_WithoutAttribute_StripsCacheItemPostfixFromFullName()
    {
        var cacheName = CacheNameAttribute.GetCacheName<PlainSampleCacheItem>();

        Assert.Equal("XiHan.Framework.Caching.Tests.PlainSample", cacheName);
        Assert.DoesNotContain("CacheItem", cacheName, StringComparison.Ordinal);
    }

    /// <summary>
    /// 未标注名称且类型名不含后缀时保留完整类型全名
    /// </summary>
    [Fact]
    public void GetCacheName_WithoutPostfix_KeepsFullName()
    {
        Assert.Equal(typeof(UnnamedPayload).FullName, CacheNameAttribute.GetCacheName<UnnamedPayload>());
    }

    /// <summary>
    /// 缓存名称沿继承链向下传递
    /// </summary>
    /// <remarks>
    /// 派生的缓存项类型应当与基类共享同一份缓存名，否则同一份数据会按派生类型被拆成多个命名空间。
    /// </remarks>
    [Fact]
    public void GetCacheName_ForDerivedType_InheritsAttributeName()
    {
        Assert.Equal("annotated-base", CacheNameAttribute.GetCacheName<DerivedFromAnnotatedBase>());
    }

    /// <summary>
    /// 特性保留构造时传入的名称
    /// </summary>
    [Fact]
    public void Constructor_KeepsProvidedName()
    {
        Assert.Equal("orders", new CacheNameAttribute("orders").Name);
    }

    /// <summary>
    /// 名称为空时拒绝构造
    /// </summary>
    [Fact]
    public void Constructor_WithNullName_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new CacheNameAttribute(null!));
    }

    /// <summary>
    /// 特性只能标注在类型上，且允许标注一次
    /// </summary>
    [Fact]
    public void AttributeUsage_TargetsTypeDeclarationsOnly()
    {
        var usage = typeof(CacheNameAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
    }

    /// <summary>
    /// 带缓存名标注的基类
    /// </summary>
    [CacheName("annotated-base")]
    private class AnnotatedBase;

    /// <summary>
    /// 从带标注基类派生的缓存项
    /// </summary>
    private sealed class DerivedFromAnnotatedBase : AnnotatedBase;

    /// <summary>
    /// 既无标注也无 CacheItem 后缀的类型
    /// </summary>
    private sealed class UnnamedPayload;
}
