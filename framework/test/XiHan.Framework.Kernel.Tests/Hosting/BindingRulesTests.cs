// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Tests.Hosting;

/// <summary>
/// 清单声明的绑定与解析结果的核对
/// </summary>
/// <remarks>
/// <c>spec/plugin/lifecycle.md</c> §2「<c>bindings</c> 的核对」（ADR-0292、ADR-0301）：先有没有、再类型；内核装配特性与三种插件宿主共用这一处（装配特性的由 <c>FeatureBindingRulesTests</c> 核对）。按 RFC-0016 解析出的绑定怎样到达这里、失败怎样成为配置或加载失败，由各宿主的测试核对
/// </remarks>
public sealed class BindingRulesTests
{
    [Fact]
    public void ARequiredBinding_WithoutContent_IsNotProvided()
    {
        // 清单没写 required 就是必需的
        var required = new Binding("db", "connection:relational");
        Assert.True(required.Required);

        // 两种来源都没给，或只给了类型（一个只有 type 文件的目录）：插件拿不到任何内容
        Assert.Equal("binding 'db' is required but not provided", BindingRules.FindViolation(required, null).ValueOrThrow());
        Assert.Equal("binding 'db' is required but not provided", BindingRules.FindViolation(required, Value("connection:relational", null)).ValueOrThrow());
        // 有主值或有属性就算给出了
        Assert.False(BindingRules.FindViolation(required, Value(string.Empty, "postgres://db/app")).HasValue);
        Assert.False(BindingRules.FindViolation(required, Value(string.Empty, null, ("host", "db.internal"))).HasValue);
        Assert.False(BindingRules.FindViolation(required, Value(string.Empty, null, ("password", string.Empty))).HasValue);
    }

    [Fact]
    public void AnOptionalBinding_WithoutContent_IsFine()
    {
        var optional = new Binding("cache", "cache:distributed") { Required = false };

        Assert.False(BindingRules.FindViolation(optional, null).HasValue);
        Assert.False(BindingRules.FindViolation(optional, Value("queue", null)).HasValue);
    }

    [Fact]
    public void ASourceDeclaringAnotherType_IsRejected_ForRequiredAndOptionalBindings()
    {
        var required = new Binding("db", "connection:relational");
        var optional = new Binding("cache", "cache:distributed") { Required = false };

        Assert.Equal("binding 'db' is of type 'connection:document' but the manifest declares 'connection:relational'", BindingRules.FindViolation(required, Value("connection:document", "mongodb://db/app", ("password", "s3cret"))).ValueOrThrow());
        // 可选的绑定一旦下发插件就会拿它去连：给错了类型同样不合
        Assert.Equal("binding 'cache' is of type 'queue' but the manifest declares 'cache:distributed'", BindingRules.FindViolation(optional, Value("queue", "amqp://mq")).ValueOrThrow());
        // 按序数比较：只差大小写也算不同
        Assert.Equal("binding 'db' is of type 'Connection:Relational' but the manifest declares 'connection:relational'", BindingRules.FindViolation(required, Value("Connection:Relational", "x")).ValueOrThrow());
    }

    [Fact]
    public void ASourceWithoutAType_OrWithTheSameType_IsFine()
    {
        var required = new Binding("db", "connection:relational");

        Assert.False(BindingRules.FindViolation(required, Value(string.Empty, "postgres://db/app")).HasValue);
        Assert.False(BindingRules.FindViolation(required, Value("connection:relational", "postgres://db/app")).HasValue);
    }

    [Fact]
    public void TheReason_NamesTheBindingAndTheTypes_ButNoValue()
    {
        var reason = BindingRules.FindViolation(new Binding("db", "connection:relational"), Value("blob", "postgres://user:s3cret@db/app", ("password", "s3cret"))).ValueOrThrow();

        Assert.DoesNotContain("s3cret", reason, StringComparison.Ordinal);
        Assert.DoesNotContain("password", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void HasContent_IsAMainValueOrAnyProperty()
    {
        Assert.True(BindingRules.HasContent(Value(string.Empty, string.Empty)));
        Assert.True(BindingRules.HasContent(Value(string.Empty, null, ("uri", "x"))));
        Assert.False(BindingRules.HasContent(Value("queue", null)));
    }

    /// <summary>
    /// 解析出的一个绑定
    /// </summary>
    private static BindingValue Value(string type, string? value, params (string Name, string Value)[] properties)
        => new() { Name = "binding", Type = type, Value = value, Properties = properties.ToDictionary(static p => p.Name, static p => p.Value, StringComparer.Ordinal) };
}
