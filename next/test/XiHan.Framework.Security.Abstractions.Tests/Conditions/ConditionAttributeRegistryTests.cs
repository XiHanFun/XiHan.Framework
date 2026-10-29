// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Security.Abstractions.Conditions;

namespace XiHan.Framework.Security.Abstractions.Tests.Conditions;

/// <summary>
/// 条件属性登记表。
/// </summary>
/// <remarks>
/// 查找顺序是契约——精确名 → 别名 → 最长匹配的族；登记冲突在构造时就抛出，不留到求值时静默选一个。
/// </remarks>
public sealed class ConditionAttributeRegistryTests
{
    [Fact]
    public void ExactName_IgnoresCaseAndWhitespace()
    {
        var registry = new ConditionAttributeRegistry([new ConditionAttributeDescriptor("subject.userName", ConditionValueKind.String)]);

        Assert.True(registry.TryResolve(" SUBJECT.USERNAME ", out var resolution));
        Assert.Equal(("subject.userName", false), (resolution.CanonicalName, resolution.IsAlias));
    }

    [Fact]
    public void Alias_ResolvesToCanonicalAndFlagsAlias()
    {
        var registry = new ConditionAttributeRegistry([new ConditionAttributeDescriptor("subject.roleCodes", ConditionValueKind.String, IsMultiValued: true, Aliases: ["subject.role"])]);

        Assert.True(registry.TryResolve("subject.role", out var resolution));
        Assert.Equal(("subject.roleCodes", true), (resolution.CanonicalName, resolution.IsAlias));
    }

    [Fact]
    public void Family_UsesLongestPrefix_AndKeepsTheOriginalName()
    {
        var registry = new ConditionAttributeRegistry(
        [
            new ConditionAttributeDescriptor("resource.*", ConditionValueKind.Number),
            new ConditionAttributeDescriptor("resource.route.*", ConditionValueKind.String),
        ]);

        Assert.True(registry.TryResolve("resource.route.id", out var route));
        Assert.Equal((ConditionValueKind.String, "resource.route.id"), (route.Descriptor.Kind, route.CanonicalName));
        Assert.True(registry.TryResolve("resource.amount", out var amount));
        Assert.Equal(ConditionValueKind.Number, amount.Descriptor.Kind);
        Assert.False(registry.TryResolve("resource.", out _));
    }

    [Fact]
    public void ExactBeatsFamily()
    {
        var registry = new ConditionAttributeRegistry(
        [
            new ConditionAttributeDescriptor("resource.*", ConditionValueKind.String),
            new ConditionAttributeDescriptor("resource.amount", ConditionValueKind.Number),
        ]);

        Assert.True(registry.TryResolve("resource.amount", out var resolution));
        Assert.Equal(ConditionValueKind.Number, resolution.Descriptor.Kind);
    }

    [Theory]
    [InlineData("subject.unknown")]
    [InlineData("")]
    [InlineData("   ")]
    public void Unknown_IsNotResolved(string name)
        => Assert.False(new ConditionAttributeRegistry([new ConditionAttributeDescriptor("subject.userName", ConditionValueKind.String)]).TryResolve(name, out _));

    [Fact]
    public void Conflicts_ThrowAtConstruction()
    {
        Assert.Throws<InvalidOperationException>(static () => new ConditionAttributeRegistry([new("a.b", ConditionValueKind.String), new("A.B", ConditionValueKind.Number)]));
        Assert.Throws<InvalidOperationException>(static () => new ConditionAttributeRegistry([new("a.b", ConditionValueKind.String), new("a.c", ConditionValueKind.String, Aliases: ["a.b"])]));
        Assert.Throws<InvalidOperationException>(static () => new ConditionAttributeRegistry([new("a.b", ConditionValueKind.String, Aliases: ["old"]), new("a.c", ConditionValueKind.String, Aliases: ["old"])]));
        Assert.Throws<InvalidOperationException>(static () => new ConditionAttributeRegistry([new("a b", ConditionValueKind.String)]));
        Assert.Throws<InvalidOperationException>(static () => new ConditionAttributeRegistry([new("a.*", ConditionValueKind.String), new("A.*", ConditionValueKind.Number)]));
    }
}
