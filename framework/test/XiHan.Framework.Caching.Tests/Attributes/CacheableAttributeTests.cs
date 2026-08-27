// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Caching.Attributes;

namespace XiHan.Framework.Caching.Tests;

/// <summary>
/// 可缓存方法特性测试
/// </summary>
/// <remarks>
/// 默认过期秒数会直接决定线上缓存的生命周期，属于对外承诺的口径，改动必须是显式的。
/// 特性用法（只能标方法、不可重复、可继承）决定了拦截器扫描时能不能找到它。
/// </remarks>
public class CacheableAttributeTests
{
    /// <summary>
    /// 默认过期时间为 300 秒
    /// </summary>
    [Fact]
    public void ExpireSeconds_DefaultsTo300()
    {
        Assert.Equal(300, new CacheableAttribute().ExpireSeconds);
    }

    /// <summary>
    /// 键模板与过期秒数可在标注上显式设置
    /// </summary>
    [Fact]
    public void Properties_AreReadBackFromDeclaration()
    {
        var attribute = typeof(Target)
            .GetMethod(nameof(Target.WithExplicitSettings))!
            .GetCustomAttribute<CacheableAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("config:{tenantId}:{key}", attribute.Key);
        Assert.Equal(60, attribute.ExpireSeconds);
    }

    /// <summary>
    /// 只写键模板时过期秒数仍取默认值
    /// </summary>
    [Fact]
    public void ExpireSeconds_WhenNotDeclared_KeepsDefault()
    {
        var attribute = typeof(Target)
            .GetMethod(nameof(Target.WithKeyOnly))!
            .GetCustomAttribute<CacheableAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(300, attribute.ExpireSeconds);
    }

    /// <summary>
    /// 特性只能标注在方法上，不可重复，且沿继承链传递
    /// </summary>
    [Fact]
    public void AttributeUsage_IsMethodOnlyNonMultipleAndInherited()
    {
        var usage = typeof(CacheableAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.True(usage.Inherited);
    }

    /// <summary>
    /// 承载标注的目标类型
    /// </summary>
    private sealed class Target
    {
        /// <summary>
        /// 显式设置了键模板与过期秒数的方法
        /// </summary>
        /// <returns>占位值</returns>
        [Cacheable(Key = "config:{tenantId}:{key}", ExpireSeconds = 60)]
        public string WithExplicitSettings()
        {
            return string.Empty;
        }

        /// <summary>
        /// 只设置了键模板的方法
        /// </summary>
        /// <returns>占位值</returns>
        [Cacheable(Key = "config:all")]
        public string WithKeyOnly()
        {
            return string.Empty;
        }
    }
}
