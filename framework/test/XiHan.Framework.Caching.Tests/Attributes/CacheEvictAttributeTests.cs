// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Reflection;
using XiHan.Framework.Caching.Attributes;

namespace XiHan.Framework.Caching.Tests.Attributes;

/// <summary>
/// 缓存清除方法特性测试
/// </summary>
/// <remarks>
/// 一次写操作往往要连带失效多个缓存键，所以该特性必须允许在同一方法上重复标注；
/// 这条一旦退化成不可重复，多余的失效声明会被静默丢弃，留下读到旧值的脏缓存。
/// </remarks>
public class CacheEvictAttributeTests
{
    /// <summary>
    /// 键模板可在标注上显式设置
    /// </summary>
    [Fact]
    public void Key_IsReadBackFromDeclaration()
    {
        var attribute = typeof(Target)
            .GetMethod(nameof(Target.EvictsOne))!
            .GetCustomAttribute<CacheEvictAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("config:{tenantId}", attribute.Key);
    }

    /// <summary>
    /// 同一方法上的多个标注全部可读取
    /// </summary>
    [Fact]
    public void MultipleAttributes_AreAllReadable()
    {
        var attributes = typeof(Target)
            .GetMethod(nameof(Target.EvictsMany))!
            .GetCustomAttributes<CacheEvictAttribute>()
            .ToArray();

        Assert.Equal(2, attributes.Length);
        Assert.Contains("config:{tenantId}", attributes.Select(attribute => attribute.Key));
        Assert.Contains("summary:{tenantId}", attributes.Select(attribute => attribute.Key));
    }

    /// <summary>
    /// 未标注的方法读不到任何清除声明
    /// </summary>
    [Fact]
    public void UnmarkedMethod_HasNoAttributes()
    {
        var attributes = typeof(Target)
            .GetMethod(nameof(Target.Plain))!
            .GetCustomAttributes<CacheEvictAttribute>();

        Assert.Empty(attributes);
    }

    /// <summary>
    /// 特性只能标注在方法上，可重复，且沿继承链传递
    /// </summary>
    [Fact]
    public void AttributeUsage_IsMethodOnlyMultipleAndInherited()
    {
        var usage = typeof(CacheEvictAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Method, usage.ValidOn);
        Assert.True(usage.AllowMultiple);
        Assert.True(usage.Inherited);
    }

    /// <summary>
    /// 承载标注的目标类型
    /// </summary>
    private sealed class Target
    {
        /// <summary>
        /// 清除单个缓存键的方法
        /// </summary>
        [CacheEvict(Key = "config:{tenantId}")]
        public void EvictsOne()
        {
        }

        /// <summary>
        /// 清除多个缓存键的方法
        /// </summary>
        [CacheEvict(Key = "config:{tenantId}")]
        [CacheEvict(Key = "summary:{tenantId}")]
        public void EvictsMany()
        {
        }

        /// <summary>
        /// 未标注清除声明的方法
        /// </summary>
        public void Plain()
        {
        }
    }
}
