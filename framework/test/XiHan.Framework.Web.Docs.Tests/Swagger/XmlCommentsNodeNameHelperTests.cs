// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Web.Docs.Tests.Swagger;

/// <summary>
/// XML 注释节点名称辅助类测试
/// </summary>
/// <remarks>
/// 这个类是整个文档模块里唯一的纯函数，也是最值得锁死的契约：它生成的成员名必须与
/// Roslyn 写进 .xml 文档文件的 id 逐字符相同，差一个字符就查不到节点，注释直接丢失。
/// 因此期望值全部按 ECMA-334 附录 E / Roslyn 的文档注释 id 规则手写，而不是照抄实现的输出。
/// 辅助类本身是 internal，经 <see cref="SwaggerInternals"/> 反射调用。
/// </remarks>
public class XmlCommentsNodeNameHelperTests
{
    private const string ServiceMemberPrefix = "M:XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleService.";

    private const string RepositoryMemberPrefix = "M:XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleRepository`1.";

    private const string GenericBaseMemberPrefix = "M:XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleGenericBase`1.";

    /// <summary>
    /// 各类参数签名生成的成员名与 Roslyn 文档 id 一致
    /// </summary>
    /// <param name="methodName">被测方法名</param>
    /// <param name="expected">期望的 XML 文档成员名</param>
    [Theory]
    [InlineData(nameof(XmlDocSampleService.NoParameters), ServiceMemberPrefix + "NoParameters")]
    [InlineData(nameof(XmlDocSampleService.Primitives), ServiceMemberPrefix + "Primitives(System.String,System.Int32)")]
    [InlineData(nameof(XmlDocSampleService.NullableValue), ServiceMemberPrefix + "NullableValue(System.Nullable{System.Int32})")]
    [InlineData(nameof(XmlDocSampleService.ByRefParameters), ServiceMemberPrefix + "ByRefParameters(System.Int32@,System.String@)")]
    [InlineData(nameof(XmlDocSampleService.InParameter), ServiceMemberPrefix + "InParameter(System.Decimal@)")]
    [InlineData(nameof(XmlDocSampleService.ArrayParameters), ServiceMemberPrefix + "ArrayParameters(System.String[],System.Int32[][])")]
    [InlineData(
        nameof(XmlDocSampleService.GenericContainers),
        ServiceMemberPrefix + "GenericContainers(System.Collections.Generic.List{System.String},System.Collections.Generic.Dictionary{System.String,System.Int32})")]
    [InlineData(
        nameof(XmlDocSampleService.NestedTypeParameter),
        ServiceMemberPrefix + "NestedTypeParameter(XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleService.NestedPayload)")]
    public void GetMemberNameForMethod_ForParameterShapes_MatchesRoslynDocumentationId(string methodName, string expected)
    {
        var method = typeof(XmlDocSampleService).GetMethod(methodName)!;

        Assert.Equal(expected, SwaggerInternals.GetMemberNameForMethod(method));
    }

    /// <summary>
    /// 泛型方法的成员名带 ``N 元数后缀
    /// </summary>
    /// <remarks>
    /// Roslyn 对 void GenericMethod&lt;TValue&gt;(TValue, IReadOnlyList&lt;TValue&gt;) 生成的 id 形如
    /// M:...GenericMethod``1(``0,System.Collections.Generic.IReadOnlyList{``0})，元数后缀是 id 的组成部分，
    /// 缺了就查不到节点。此处按正确语义断言，实现的偏差见交付报告的「疑似缺陷」。
    /// </remarks>
    [Fact]
    public void GetMemberNameForMethod_WhenGenericMethod_AppendsGenericArity()
    {
        var method = typeof(XmlDocSampleService).GetMethod("GenericMethod")!;

        Assert.Equal(
            ServiceMemberPrefix + "GenericMethod``1(``0,System.Collections.Generic.IReadOnlyList{``0})",
            SwaggerInternals.GetMemberNameForMethod(method));
    }

    /// <summary>
    /// 开放泛型声明类型保留反引号元数，类型参数写成位置索引
    /// </summary>
    [Fact]
    public void GetMemberNameForMethod_WhenOpenGenericDeclaringType_UsesArityAndTypeParameterIndex()
    {
        var method = typeof(XmlDocSampleRepository<>).GetMethod("Save")!;

        Assert.Equal(RepositoryMemberPrefix + "Save(`0)", SwaggerInternals.GetMemberNameForMethod(method));
    }

    /// <summary>
    /// 闭合泛型类型上的方法先给闭合签名，再补开放定义签名
    /// </summary>
    /// <remarks>
    /// 顺序是契约的一部分：闭合签名优先，查不到时才回落到开放定义；
    /// 而 Roslyn 实际写进 xml 的是开放定义那一条，所以两条都必须在候选里。
    /// </remarks>
    [Fact]
    public void GetMemberNamesForMethod_WhenConstructedGenericDeclaringType_YieldsClosedThenOpenCandidate()
    {
        var method = typeof(XmlDocSampleRepository<string>).GetMethod("Save")!;

        Assert.Equal(
            new[]
            {
                RepositoryMemberPrefix + "Save(System.String)",
                RepositoryMemberPrefix + "Save(`0)"
            },
            SwaggerInternals.GetMemberNamesForMethod(method).ToArray());
    }

    /// <summary>
    /// 候选名重复时只保留一条
    /// </summary>
    [Fact]
    public void GetMemberNamesForMethod_WhenCandidatesCollide_Deduplicates()
    {
        var method = typeof(XmlDocSampleRepository<string>).GetMethod("Ping")!;

        Assert.Equal(RepositoryMemberPrefix + "Ping", Assert.Single(SwaggerInternals.GetMemberNamesForMethod(method)));
    }

    /// <summary>
    /// 重写方法在自身之后补上基类声明，用于继承来的注释
    /// </summary>
    [Fact]
    public void GetMemberNamesForMethod_WhenOverride_AppendsBaseDefinitionCandidate()
    {
        var method = typeof(XmlDocSampleDerived).GetMethod(nameof(XmlDocSampleDerived.Run))!;

        Assert.Equal(
            new[]
            {
                "M:XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleDerived.Run(System.String)",
                "M:XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleBase.Run(System.String)"
            },
            SwaggerInternals.GetMemberNamesForMethod(method).ToArray());
    }

    /// <summary>
    /// 重写泛型基类方法时，候选里必须出现基类的开放定义签名
    /// </summary>
    /// <remarks>
    /// 这正是源码注释所说的「兼容泛型基类方法」：注释写在 XmlDocSampleGenericBase&lt;TEntity&gt;.Handle 上，
    /// Roslyn 落在 xml 里的是 Handle(`0)，而运行期拿到的是闭合后的 Handle(System.String)。
    /// </remarks>
    [Fact]
    public void GetMemberNamesForMethod_WhenGenericBaseOverride_YieldsOpenBaseCandidate()
    {
        var method = typeof(XmlDocSampleStringHandler).GetMethod(nameof(XmlDocSampleStringHandler.Handle))!;

        var memberNames = SwaggerInternals.GetMemberNamesForMethod(method);

        Assert.Equal(
            "M:XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleStringHandler.Handle(System.String)",
            memberNames[0]);
        Assert.Contains(GenericBaseMemberPrefix + "Handle(System.String)", memberNames);
        Assert.Contains(GenericBaseMemberPrefix + "Handle(`0)", memberNames);
    }

    /// <summary>
    /// 单值接口返回候选列表的首个元素
    /// </summary>
    [Fact]
    public void GetMemberNameForMethod_ReturnsFirstCandidateOfMemberNames()
    {
        var method = typeof(XmlDocSampleDerived).GetMethod(nameof(XmlDocSampleDerived.Run))!;

        Assert.Equal(
            SwaggerInternals.GetMemberNamesForMethod(method)[0],
            SwaggerInternals.GetMemberNameForMethod(method));
    }

    /// <summary>
    /// 类型成员名带 T 前缀，嵌套类型用点号连接
    /// </summary>
    /// <param name="type">被测类型</param>
    /// <param name="expected">期望的 XML 文档成员名</param>
    [Theory]
    [InlineData(typeof(XmlDocSampleService), "T:XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleService")]
    [InlineData(typeof(XmlDocSampleService.NestedPayload), "T:XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleService.NestedPayload")]
    [InlineData(typeof(int), "T:System.Int32")]
    [InlineData(typeof(List<string>), "T:System.Collections.Generic.List`1")]
    public void GetMemberNameForType_MatchesRoslynDocumentationId(Type type, string expected)
    {
        Assert.Equal(expected, SwaggerInternals.GetMemberNameForType(type));
    }

    /// <summary>
    /// 开放泛型类型本身也归一到泛型定义名
    /// </summary>
    [Fact]
    public void GetMemberNameForType_WhenOpenGeneric_KeepsGenericTypeDefinitionName()
    {
        Assert.Equal(
            "T:XiHan.Framework.Web.Docs.Tests.Swagger.XmlDocSampleRepository`1",
            SwaggerInternals.GetMemberNameForType(typeof(XmlDocSampleRepository<>)));
    }

    /// <summary>
    /// 闭合泛型与开放泛型归一到同一个类型成员名
    /// </summary>
    [Fact]
    public void GetMemberNameForType_ForConstructedAndOpenGeneric_AreIdentical()
    {
        Assert.Equal(
            SwaggerInternals.GetMemberNameForType(typeof(XmlDocSampleRepository<>)),
            SwaggerInternals.GetMemberNameForType(typeof(XmlDocSampleRepository<string>)));
    }
}
