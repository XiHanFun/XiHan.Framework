// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace XiHan.Framework.Testing.Workspace;

/// <summary>
/// 定位并严格读取规范的一致性向量
/// </summary>
/// <remarks>
/// 向量在框架工作区的 <c>spec/conformance/vectors/&lt;目录&gt;/&lt;文件&gt;</c>（规范子模块，RFC-0024），参考实现与各语言 SDK 回放同一份文件，测试只回放、不另存副本。读取一律严格：成员名按 Web 缺省（camelCase、不区分大小写），对象模型要认得文件里的每个成员（顶层给人看的 <c>$comment</c> 除外），模型里没有缺省值的构造参数与 <see langword="required"/> 成员都要出现在文件里，同一对象里的成员不能重名，<see langword="null"/> 只能落到声明为可空的成员上（属性、字段与构造参数；集合的元素不在此列，STJ 不看泛型实参的可空性）。向量改了写法时读取就失败，不会漏掉字段还照样通过。向量只在框架仓库的检出里找得到，所以放在不发包的测试类库里（ADR-0300）
/// </remarks>
public static class SpecVectors
{
    /// <summary>
    /// 向量文件顶部给人看的说明成员
    /// </summary>
    private const string CommentMember = "$comment";

    /// <summary>
    /// 反射读取用的选项，第一次用到时创建
    /// </summary>
    private static JsonSerializerOptions? _reflection;

    /// <summary>
    /// 向量根目录的绝对路径
    /// </summary>
    /// <remarks>
    /// 即框架工作区下的 <c>spec/conformance/vectors</c>
    /// </remarks>
    /// <exception cref="InvalidOperationException">找不到框架工作区（见 <see cref="FrameworkWorkspace.Root"/>）</exception>
    public static string Directory => Path.Combine(FrameworkWorkspace.Root, "spec", "conformance", "vectors");

    /// <summary>
    /// 一个向量文件的绝对路径
    /// </summary>
    /// <remarks>
    /// 只拼路径、不核对文件在不在：要按 <see cref="JsonDocument"/> 自己读或写回向量的测试用它
    /// </remarks>
    /// <param name="area"><c>conformance/vectors</c> 下的目录名，如 <c>caching</c></param>
    /// <param name="fileName">该目录下的文件名，如 <c>cache-keys.json</c></param>
    /// <exception cref="ArgumentException">目录名或文件名为空、含路径分隔符或是 <c>.</c> / <c>..</c></exception>
    public static string PathOf(string area, string fileName)
    {
        ThrowIfNotOneSegment(area);
        ThrowIfNotOneSegment(fileName);
        return Path.Combine(Directory, area, fileName);
    }

    /// <summary>
    /// 按反射严格读取一个向量文件
    /// </summary>
    /// <typeparam name="T">向量的对象模型</typeparam>
    /// <param name="area"><c>conformance/vectors</c> 下的目录名</param>
    /// <param name="fileName">该目录下的文件名</param>
    /// <returns>读出的对象</returns>
    /// <exception cref="ArgumentException">目录名或文件名不是单独一段（见 <see cref="PathOf"/>）</exception>
    /// <exception cref="FileNotFoundException">向量文件不存在</exception>
    /// <exception cref="InvalidOperationException">文件是空的，或顶层不是 JSON 对象</exception>
    /// <exception cref="JsonException">文件不是合法的 JSON，或与对象模型对不上</exception>
    [RequiresUnreferencedCode("Reflection-based deserialization requires the members of the vector model to be preserved.")]
    [RequiresDynamicCode("Reflection-based deserialization may require runtime code generation.")]
    public static T Read<T>(string area, string fileName)
    {
        var options = Volatile.Read(ref _reflection);
        if (options is null)
        {
            // 并发的第一次读取可能各建一份，留下先写入的那份：选项读过之后不可变，哪份都一样
            options = CreateOptions(new DefaultJsonTypeInfoResolver());
            options = Interlocked.CompareExchange(ref _reflection, options, null) ?? options;
        }

        return ReadFile(PathOf(area, fileName), TypeInfoOf<T>(options));
    }

    /// <summary>
    /// 按给定的类型信息来源严格读取一个向量文件
    /// </summary>
    /// <remarks>
    /// <paramref name="resolver"/> 通常是源生成的 <see cref="JsonSerializerContext"/>：只取它的类型信息，读法照旧是严格的，与它自己的选项无关
    /// </remarks>
    /// <typeparam name="T">向量的对象模型</typeparam>
    /// <param name="area"><c>conformance/vectors</c> 下的目录名</param>
    /// <param name="fileName">该目录下的文件名</param>
    /// <param name="resolver">提供 <typeparamref name="T"/> 的类型信息</param>
    /// <returns>读出的对象</returns>
    /// <exception cref="ArgumentException">目录名或文件名不是单独一段（见 <see cref="PathOf"/>）</exception>
    /// <exception cref="FileNotFoundException">向量文件不存在</exception>
    /// <exception cref="InvalidOperationException">文件是空的，或顶层不是 JSON 对象</exception>
    /// <exception cref="JsonException">文件不是合法的 JSON，或与对象模型对不上</exception>
    /// <exception cref="NotSupportedException"><paramref name="resolver"/> 不认得 <typeparamref name="T"/></exception>
    public static T Read<T>(string area, string fileName, IJsonTypeInfoResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        var path = PathOf(area, fileName);
        return ReadFile(path, TypeInfoOf<T>(CreateOptions(resolver)));
    }

    /// <summary>
    /// 由向量里以四位十六进制写出的 UTF-16 码元组成字符串
    /// </summary>
    /// <remarks>
    /// JSON 字符串写不出孤立的代理项（System.Text.Json 读到转义出的孤立代理项即抛出），含它的标识在向量里改以 <c>utf16</c> 列出码元（RFC-0012「一致性」，ADR-0316）。码元原样组成，成对的代理项因此就是平面以外的那个字符
    /// </remarks>
    /// <param name="codeUnits">码元，每项四位十六进制，不区分大小写</param>
    /// <returns>组成的字符串，可能含孤立的代理项</returns>
    /// <exception cref="FormatException">有一项不是四位十六进制</exception>
    public static string FromCodeUnits(IReadOnlyList<string> codeUnits)
    {
        ArgumentNullException.ThrowIfNull(codeUnits);
        var text = new char[codeUnits.Count];
        for (var index = 0; index < codeUnits.Count; index++)
        {
            var unit = codeUnits[index];
            // 只认四位十六进制：向量写错了要在这里失败，不能读成另一个文本
            if (unit is not { Length: 4 } || !ushort.TryParse(unit, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"The code unit '{unit}' at index {index} must be four hexadecimal digits.");
            }

            text[index] = (char)value;
        }

        return new string(text);
    }

    /// <summary>
    /// 一条用例的文本：原文，或由以码元写出的 <c>utf16</c> 组成
    /// </summary>
    /// <remarks>
    /// 写得进 JSON 字符串的文本写原文，写不进的（含孤立的代理项）写码元，一条用例恰好给出其中一样（ADR-0316）
    /// </remarks>
    /// <param name="name">用例的名字，写进错误消息</param>
    /// <param name="text">原文；没写为 <see langword="null"/></param>
    /// <param name="codeUnits">码元；没写为 <see langword="null"/></param>
    /// <returns>用例的文本</returns>
    /// <exception cref="InvalidOperationException">原文与码元都写了或都没写</exception>
    /// <exception cref="FormatException">码元有一项不是四位十六进制（见 <see cref="FromCodeUnits"/>）</exception>
    public static string TextOf(string name, string? text, IReadOnlyList<string>? codeUnits) => (text, codeUnits) switch
    {
        ({ } original, null) => original,
        (null, { } units) => FromCodeUnits(units),
        _ => throw new InvalidOperationException($"The case '{name}' must give either its text or its 'utf16' code units, not both or neither."),
    };

    /// <summary>
    /// 严格读取给定路径的 JSON 文件
    /// </summary>
    /// <typeparam name="T">对象模型</typeparam>
    /// <param name="path">文件的路径</param>
    /// <param name="typeInfo">由 <see cref="CreateOptions"/> 的选项给出的类型信息</param>
    internal static T ReadFile<T>(string path, JsonTypeInfo<T> typeInfo)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The vector file '{path}' does not exist; check that the spec submodule is checked out.", path);
        }

        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException($"The vector file '{path}' is empty.");
        }

        try
        {
            // 重名成员在解析时就拒绝：哪一个算数不该取决于读法
            var node = JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { AllowDuplicateProperties = false });
            if (node is not JsonObject document)
            {
                throw new InvalidOperationException(node is null ? $"The vector file '{path}' is empty: it holds only 'null'." : $"The vector file '{path}' is not a JSON object.");
            }

            // 只去掉顶层的说明：别处的同名成员属于向量的内容，模型不认得就该失败
            document.Remove(CommentMember);
            return document.Deserialize(typeInfo) ?? throw new InvalidOperationException($"The vector file '{path}' is empty: it was read as 'null'.");
        }
        catch (JsonException exception)
        {
            throw new JsonException($"The vector file '{path}' does not match '{typeof(T).Name}': {exception.Message}", exception.Path, exception.LineNumber, exception.BytePositionInLine, exception);
        }
    }

    /// <summary>
    /// 以给定的类型信息来源创建严格读取的选项
    /// </summary>
    /// <param name="resolver">类型信息来源</param>
    internal static JsonSerializerOptions CreateOptions(IJsonTypeInfoResolver resolver)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = resolver,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            // 向量里的 null 落到不可空的成员上就失败：模型的可空性按向量的事实写，测试不会把 null 当成字符串或对象往下用
            RespectNullableAnnotations = true,
            AllowDuplicateProperties = false,
        };
        options.MakeReadOnly();
        return options;
    }

    /// <summary>
    /// 取选项里某个类型的类型信息
    /// </summary>
    /// <typeparam name="T">对象模型</typeparam>
    /// <param name="options">严格读取的选项</param>
    private static JsonTypeInfo<T> TypeInfoOf<T>(JsonSerializerOptions options) => (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));

    /// <summary>
    /// 核对目录名或文件名是单独一段
    /// </summary>
    /// <param name="segment">目录名或文件名</param>
    /// <param name="name">实参的名字</param>
    private static void ThrowIfNotOneSegment(string segment, [CallerArgumentExpression(nameof(segment))] string? name = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segment, name);
        if (segment is "." or ".." || segment.IndexOfAny(['/', '\\']) >= 0 || segment != Path.GetFileName(segment))
        {
            throw new ArgumentException($"'{segment}' must be a single directory or file name under 'conformance/vectors'.", name);
        }
    }
}
