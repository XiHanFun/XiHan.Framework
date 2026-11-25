// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using XiHan.Framework.Kernel.Json;

namespace XiHan.Framework.Kernel.Tests.Json;

/// <summary>
/// 共同的 JSON 读取检查报出的种类、先后与形状
/// </summary>
/// <remarks>
/// 向量只分语法错误与重复成员（<see cref="JsonTextVectorTests"/>），这里核对 .NET 把语法错误细分出的种类与它们之间的先后（ADR-0291）
/// </remarks>
public sealed class JsonTextTests
{
    [Theory]
    [InlineData("", JsonTextProblemKind.Syntax)]
    [InlineData("   ", JsonTextProblemKind.Syntax)]
    [InlineData("""{"a":1,}""", JsonTextProblemKind.Syntax)]
    [InlineData("""{"a":1} x""", JsonTextProblemKind.Syntax)]
    [InlineData("""{"a":01}""", JsonTextProblemKind.Syntax)]
    [InlineData("\uFEFF{}", JsonTextProblemKind.Syntax)]
    [InlineData("{\"a\":\"x\ty\"}", JsonTextProblemKind.Syntax)]
    [InlineData("""{"a":"\q"}""", JsonTextProblemKind.Syntax)]
    [InlineData("""{"a":"\ud800"}""", JsonTextProblemKind.LoneSurrogate)]
    [InlineData("""{"a":"\udc00"}""", JsonTextProblemKind.LoneSurrogate)]
    [InlineData("""{"a":"\ud800\u0041"}""", JsonTextProblemKind.LoneSurrogate)]
    [InlineData("""{"\ud800":1}""", JsonTextProblemKind.LoneSurrogate)]
    public void FindProblem_ReportsTheKindOfASyntaxError_AtTheRoot(string text, JsonTextProblemKind kind)
    {
        var problem = JsonText.FindProblem(Encoding.UTF8.GetBytes(text)).ValueOrThrow();

        Assert.Equal(new JsonTextProblem { Kind = kind, Path = "$" }, problem);
    }

    [Theory]
    [InlineData("7b2261223a22ff227d")]
    [InlineData("7b2261223a22c0af227d")]
    [InlineData("7b2261223a22eda080227d")]
    [InlineData("7b2261223a22e4b8227d")]
    [InlineData("7b22ff223a317d")]
    [InlineData("7b2261223a225c6eff227d")]
    public void FindProblem_ReportsInvalidUtf8InStringsAndNames(string hex)
    {
        // 最后一条是带转义的字符串里的非法字节：先核对原始字节，不报成代理项转义
        var problem = JsonText.FindProblem(Convert.FromHexString(hex)).ValueOrThrow();

        Assert.Equal(new JsonTextProblem { Kind = JsonTextProblemKind.InvalidUtf8, Path = "$" }, problem);
    }

    [Fact]
    public void FindProblem_ReportsIllegalBytesOutsideStringsAsSyntax()
    {
        Assert.Equal(JsonTextProblemKind.Syntax, JsonText.FindProblem(Convert.FromHexString("7b7dff")).ValueOrThrow().Kind);
        Assert.Equal(JsonTextProblemKind.Syntax, JsonText.FindProblem(Convert.FromHexString("7b007d00")).ValueOrThrow().Kind);
    }

    [Fact]
    public void FindProblem_ReportsTheSixtyFifthLevelAsNestingTooDeep()
    {
        Assert.False(JsonText.FindProblem(Nested(JsonText.MaxDepth)).HasValue);
        Assert.Equal(new JsonTextProblem { Kind = JsonTextProblemKind.NestingTooDeep, Path = "$" }, JsonText.FindProblem(Nested(JsonText.MaxDepth + 1)).ValueOrThrow());

        // 对象与数组同样计层；第 65 层之后没有闭合也先报嵌套过深，因为它在文本里先出现
        var objects = string.Concat(Enumerable.Repeat("""{"a":""", JsonText.MaxDepth)) + "{";
        Assert.Equal(JsonTextProblemKind.NestingTooDeep, JsonText.FindProblem(Encoding.UTF8.GetBytes(objects)).ValueOrThrow().Kind);
    }

    [Theory]
    [InlineData("""{"a":1,"a":2}""", "$.a")]
    [InlineData("""{"a":1,"\u0061":2}""", "$.a")]
    [InlineData("""{"x":[{"b":1},{"b":1,"b":2}]}""", "$.x[1].b")]
    [InlineData("""{"a.b":{"c":1,"c":2}}""", "$['a.b'].c")]
    [InlineData("""{"a":1,"a":2,"b":{"c":1,"c":2}}""", "$.a")]
    public void FindProblem_ReportsTheFirstDuplicateMember_ByItsPath(string text, string path)
    {
        var problem = JsonText.FindProblem(Encoding.UTF8.GetBytes(text)).ValueOrThrow();

        Assert.Equal(new JsonTextProblem { Kind = JsonTextProblemKind.DuplicateMember, Path = path }, problem);
    }

    [Theory]
    [InlineData("""{"a":1,"a":2,"b":"\ud800"}""", JsonTextProblemKind.LoneSurrogate)]
    [InlineData("""{"a":1,"a":2,"b":01}""", JsonTextProblemKind.Syntax)]
    [InlineData("""{"a":"\ud800","b":01}""", JsonTextProblemKind.LoneSurrogate)]
    [InlineData("""{"a":01,"b":"\ud800"}""", JsonTextProblemKind.Syntax)]
    public void FindProblem_PutsSyntaxErrorsBeforeDuplicates_AndReportsTheFirstSyntaxError(string text, JsonTextProblemKind kind)
    {
        // 语法错误不论出现在哪里都优先于重复成员；几种语法错误之间按出现的先后
        var problem = JsonText.FindProblem(Encoding.UTF8.GetBytes(text)).ValueOrThrow();

        Assert.Equal(new JsonTextProblem { Kind = kind, Path = "$" }, problem);
    }

    [Fact]
    public void FindProblem_ReportsTheFirstSyntaxError_BetweenNestingAndText()
    {
        var deepAfterSurrogate = Encoding.UTF8.GetBytes("""["\ud800",""" + Encoding.UTF8.GetString(Nested(JsonText.MaxDepth)) + "]");
        var surrogateAfterDeep = Encoding.UTF8.GetBytes(new string('[', JsonText.MaxDepth + 1) + """ "\ud800" """ + new string(']', JsonText.MaxDepth + 1));

        Assert.Equal(JsonTextProblemKind.LoneSurrogate, JsonText.FindProblem(deepAfterSurrogate).ValueOrThrow().Kind);
        Assert.Equal(JsonTextProblemKind.NestingTooDeep, JsonText.FindProblem(surrogateAfterDeep).ValueOrThrow().Kind);
    }

    [Fact]
    public void FindProblem_HasNoValue_ForValidText()
    {
        Assert.False(JsonText.FindProblem("""{"a":{"a":1},"b":["\ud83d\ude00"],"A":1}"""u8).HasValue);
        Assert.False(JsonText.FindProblem("null"u8).HasValue);
    }

    [Fact]
    public void JsonTextProblem_IsBuiltByInitializer_WithBothMembersRequired()
    {
        // 位置构造与 Deconstruct 的签名随成员数变：只留无参构造加初始化成员；是引用类型，没有路径为 null 的 default 实例（ADR-0291）
        var type = typeof(JsonTextProblem);
        Assert.False(type.IsValueType);
        var constructor = Assert.Single(type.GetConstructors());
        Assert.Empty(constructor.GetParameters());
        Assert.Null(type.GetMethod("Deconstruct"));
        Assert.Equal(
            [nameof(JsonTextProblem.Kind), nameof(JsonTextProblem.Path)],
            type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(static p => p.IsDefined(typeof(RequiredMemberAttribute), inherit: false)).Select(static p => p.Name).Order(StringComparer.Ordinal));

        var problem = new JsonTextProblem { Kind = JsonTextProblemKind.DuplicateMember, Path = "$.a" };
        Assert.Equal(new JsonTextProblem { Kind = JsonTextProblemKind.DuplicateMember, Path = "$.b" }, problem with { Path = "$.b" });
        Assert.NotEqual(problem, problem with { Kind = JsonTextProblemKind.Syntax });
    }

    [Fact]
    public void JsonTextProblemKind_ValuesAreFrozen()
    {
        // 枚举常量编译进调用方：已有成员的名字与值不变，新种类只往后加
        Assert.Equal(
            [("Syntax", 0), ("NestingTooDeep", 1), ("InvalidUtf8", 2), ("LoneSurrogate", 3), ("DuplicateMember", 4)],
            Enum.GetValues<JsonTextProblemKind>().Select(static kind => (kind.ToString(), (int)kind)));
    }

    /// <summary>
    /// 嵌套给定层数的空数组
    /// </summary>
    /// <param name="depth">层数</param>
    private static byte[] Nested(int depth) => Encoding.UTF8.GetBytes(new string('[', depth) + new string(']', depth));
}
