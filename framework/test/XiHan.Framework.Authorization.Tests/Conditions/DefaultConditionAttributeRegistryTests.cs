// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Authorization.Conditions;

namespace XiHan.Framework.Authorization.Tests.Conditions;

/// <summary>
/// 条件属性登记表测试
/// </summary>
/// <remarks>
/// 查找顺序是契约：精确名 → 别名 → 最长匹配的族；登记冲突在构造时就抛出，不留到求值时静默选一个。
/// </remarks>
public class DefaultConditionAttributeRegistryTests
{
    /// <summary>
    /// 精确名不区分大小写，规范名取登记名
    /// </summary>
    [Fact]
    public void TryResolve_ExactName_IgnoresCase()
    {
        var registry = new DefaultConditionAttributeRegistry([new ConditionAttributeDescriptor("subject.userName", ConditionValueKind.String)]);

        Assert.True(registry.TryResolve(" SUBJECT.USERNAME ", out var resolution));
        Assert.Equal("subject.userName", resolution.CanonicalName);
        Assert.False(resolution.IsAlias);
    }

    /// <summary>
    /// 别名解析到登记名并标出用了别名
    /// </summary>
    [Fact]
    public void TryResolve_Alias_ResolvesToCanonicalAndFlagsAlias()
    {
        var registry = new DefaultConditionAttributeRegistry(
            [new ConditionAttributeDescriptor("subject.roleCodes", ConditionValueKind.String, IsMultiValued: true, Aliases: ["subject.role"])]);

        Assert.True(registry.TryResolve("subject.role", out var resolution));
        Assert.Equal("subject.roleCodes", resolution.CanonicalName);
        Assert.True(resolution.IsAlias);
    }

    /// <summary>
    /// 族按最长前缀匹配，规范名保留原名；只有前缀本身不算
    /// </summary>
    [Fact]
    public void TryResolve_Family_UsesLongestPrefix()
    {
        var registry = new DefaultConditionAttributeRegistry(
        [
            new ConditionAttributeDescriptor("resource.*", ConditionValueKind.Number),
            new ConditionAttributeDescriptor("resource.query.*", ConditionValueKind.String)
        ]);

        Assert.True(registry.TryResolve("resource.query.id", out var query));
        Assert.Equal(ConditionValueKind.String, query.Descriptor.Kind);
        Assert.Equal("resource.query.id", query.CanonicalName);
        Assert.True(registry.TryResolve("resource.amount", out var amount));
        Assert.Equal(ConditionValueKind.Number, amount.Descriptor.Kind);
        Assert.False(registry.TryResolve("resource.", out _));
    }

    /// <summary>
    /// 精确登记优先于族
    /// </summary>
    [Fact]
    public void TryResolve_ExactBeatsFamily()
    {
        var registry = new DefaultConditionAttributeRegistry(
        [
            new ConditionAttributeDescriptor("resource.*", ConditionValueKind.String),
            new ConditionAttributeDescriptor("resource.amount", ConditionValueKind.Number)
        ]);

        Assert.True(registry.TryResolve("resource.amount", out var resolution));
        Assert.Equal(ConditionValueKind.Number, resolution.Descriptor.Kind);
    }

    /// <summary>
    /// 未登记、空名都查不到
    /// </summary>
    [Theory]
    [InlineData("subject.unknown")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_Unknown_ReturnsFalse(string name)
    {
        var registry = new DefaultConditionAttributeRegistry([new ConditionAttributeDescriptor("subject.userName", ConditionValueKind.String)]);

        Assert.False(registry.TryResolve(name, out _));
    }

    /// <summary>
    /// 重名、别名撞登记名或撞别的别名、空名都在构造时抛出
    /// </summary>
    [Fact]
    public void Constructor_Conflicts_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => new DefaultConditionAttributeRegistry(
        [
            new ConditionAttributeDescriptor("a.b", ConditionValueKind.String),
            new ConditionAttributeDescriptor("A.B", ConditionValueKind.Number)
        ]));
        Assert.Throws<InvalidOperationException>(() => new DefaultConditionAttributeRegistry(
        [
            new ConditionAttributeDescriptor("a.b", ConditionValueKind.String),
            new ConditionAttributeDescriptor("a.c", ConditionValueKind.String, Aliases: ["a.b"])
        ]));
        Assert.Throws<InvalidOperationException>(() => new DefaultConditionAttributeRegistry(
        [
            new ConditionAttributeDescriptor("a.b", ConditionValueKind.String, Aliases: ["old"]),
            new ConditionAttributeDescriptor("a.c", ConditionValueKind.String, Aliases: ["old"])
        ]));
        Assert.Throws<InvalidOperationException>(() => new DefaultConditionAttributeRegistry([new ConditionAttributeDescriptor("a b", ConditionValueKind.String)]));
        Assert.Throws<InvalidOperationException>(() => new DefaultConditionAttributeRegistry(
        [
            new ConditionAttributeDescriptor("a.*", ConditionValueKind.String),
            new ConditionAttributeDescriptor("A.*", ConditionValueKind.Number)
        ]));
    }
}
