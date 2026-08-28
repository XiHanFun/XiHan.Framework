// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Linq.Expressions;
using XiHan.Framework.Utils.Reflections;

namespace XiHan.Framework.Utils.Tests.Reflections;

/// <summary>
/// 属性信息扩展方法测试
/// </summary>
/// <remarks>
/// GetPropertyInfo 的命名回退链里含有短横线策略，单字母属性名会触发其内部越界，
/// 因此这里一律使用多字母属性名，相关问题见交付报告的疑似缺陷段落。
/// </remarks>
public class PropertyInfoExtensionsTests
{
    /// <summary>
    /// 虚属性判为 virtual，普通属性不是
    /// </summary>
    [Fact]
    public void IsVirtual_DetectsVirtualAccessor()
    {
        Assert.True(typeof(Holder).GetProperty(nameof(Holder.VirtualProperty))!.IsVirtual());
        Assert.False(typeof(Holder).GetProperty(nameof(Holder.PlainProperty))!.IsVirtual());
    }

    /// <summary>
    /// 属性本身就是 object 类型时，表达式体直接是成员访问
    /// </summary>
    [Fact]
    public void GetPropertyName_FromDirectMemberAccess_ReturnsName()
    {
        Expression<Func<Holder, object>> selector = x => x.Payload;

        Assert.Equal(nameof(Holder.Payload), selector.GetPropertyName());
    }

    /// <summary>
    /// 属性需要装箱或引用转换时，表达式体被包了一层转换节点，仍能取到名字
    /// </summary>
    [Fact]
    public void GetPropertyName_FromConvertedMemberAccess_ReturnsName()
    {
        Expression<Func<Holder, object>> stringSelector = x => x.Name;
        Expression<Func<Holder, object>> intSelector = x => x.Age;

        Assert.Equal(nameof(Holder.Name), stringSelector.GetPropertyName());
        Assert.Equal(nameof(Holder.Age), intSelector.GetPropertyName());
    }

    /// <summary>
    /// 表达式体不是成员访问时抛无效操作异常
    /// </summary>
    [Fact]
    public void GetPropertyName_WhenBodyIsNotMemberAccess_Throws()
    {
        Expression<Func<Holder, object>> selector = x => new object();

        Assert.Throws<InvalidOperationException>(() => selector.GetPropertyName());
    }

    /// <summary>
    /// 名称完全匹配时直接取到属性
    /// </summary>
    [Fact]
    public void GetPropertyInfo_WithExactName_ReturnsProperty()
    {
        var property = typeof(Holder).GetPropertyInfo(nameof(Holder.Name));

        Assert.Equal(nameof(Holder.Name), property.Name);
    }

    /// <summary>
    /// 小驼峰名称会按帕斯卡策略回退命中
    /// </summary>
    [Fact]
    public void GetPropertyInfo_WithCamelCaseName_FallsBackToPascalCase()
    {
        var property = typeof(Holder).GetPropertyInfo("displayName");

        Assert.Equal(nameof(Holder.DisplayName), property.Name);
    }

    /// <summary>
    /// 所有命名策略都不命中时抛参数异常
    /// </summary>
    [Fact]
    public void GetPropertyInfo_WhenNothingMatches_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => typeof(Holder).GetPropertyInfo("NotExistsAtAll"));

        Assert.Contains("NotExistsAtAll", ex.Message);
    }

    /// <summary>
    /// 测试用承载类型
    /// </summary>
    private class Holder
    {
        /// <summary>
        /// 虚属性
        /// </summary>
        public virtual string VirtualProperty { get; set; } = string.Empty;

        /// <summary>
        /// 普通属性
        /// </summary>
        public string PlainProperty { get; set; } = string.Empty;

        /// <summary>
        /// object 类型属性，用于验证无转换节点的表达式
        /// </summary>
        public object Payload { get; set; } = new();

        /// <summary>
        /// 字符串属性
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 值类型属性
        /// </summary>
        public int Age { get; set; }

        /// <summary>
        /// 多词属性，用于验证命名回退
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
    }
}
