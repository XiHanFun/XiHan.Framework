// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Unicode;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// XiHan 读取 JSON 文本的共同检查
/// </summary>
/// <remarks>
/// 操作载荷、清单与消息信封在交给反序列化之前都先过这一遍（RFC-0001「JSON 文本」，ADR-0236）：语法按 RFC 8259（不允许注释、尾逗号、BOM 与多个根值），嵌套不超过 64 层，字符串与成员名必须是合法的 Unicode（没有非法 UTF-8，没有孤立的代理项转义），任何一层都不能有重复的成员名。
/// 语法错误不论出现在哪里都优先于重复成员，所以遇到重复成员也读到末尾；各语言的解析器报告语法错误的位置不同，这里统一只给 <c>$</c>。
/// 公开给内核之外读 XiHan JSON 文本的包共用（消息信封的结构化 JSON，ADR-0283）：同一道检查只有这一份实现。规则由规范定死，所以没有选项（ADR-0291）
/// </remarks>
[ApiLevel(Stability.Stable, "5.0")]
public static class JsonText
{
    /// <summary>
    /// 最大嵌套层数
    /// </summary>
    /// <remarks>
    /// 最外层的对象或数组是第 1 层。检查通过之后再交给 <see cref="JsonDocument"/> 的调用方用它作 <see cref="JsonDocumentOptions.MaxDepth"/>。
    /// 值由规范定死，常量会编译进调用方
    /// </remarks>
    public const int MaxDepth = 64;

    /// <summary>
    /// 找出 UTF-8 JSON 文本没有通过检查的第一个问题
    /// </summary>
    /// <remarks>
    /// 有语法错误（<see cref="JsonTextProblemKind"/> 除 <see cref="JsonTextProblemKind.DuplicateMember"/> 之外的种类）时报文本里第一处语法错误，路径是 <c>$</c>；没有语法错误时报第一处重复成员。
    /// 文本按 UTF-8 读：.NET 字符串由调用方先编码，编码不出的（孤立的代理项字符）由调用方按语法错误处理
    /// </remarks>
    /// <param name="utf8">UTF-8 编码的文本，不带 BOM</param>
    /// <returns>通过时无值；否则是第一个问题</returns>
    public static Maybe<JsonTextProblem> FindProblem(ReadOnlySpan<byte> utf8)
    {
        // 读取器的上限比规范多一层：第 65 层由这里报成嵌套过深，而不是被读取器当成一般的语法错误
        var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = MaxDepth + 1 });
        var frames = new Stack<Frame>();
        string? duplicate = null;
        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                    case JsonTokenType.StartArray:
                        if (frames.Count == MaxDepth)
                        {
                            return Syntax(JsonTextProblemKind.NestingTooDeep);
                        }

                        frames.Push(new Frame(ValuePath(frames, duplicate is null), reader.TokenType == JsonTokenType.StartObject ? new HashSet<string>(StringComparer.Ordinal) : null));
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        frames.Pop();
                        Advance(frames);
                        break;
                    case JsonTokenType.PropertyName:
                        if (FindTextProblem(ref reader) is { } nameProblem)
                        {
                            return Syntax(nameProblem);
                        }

                        // 核对过的名字取值不会再失败；重复成员按还原转义后的名字比较
                        var name = reader.GetString()!;
                        var frame = frames.Peek();
                        frame.PendingName = name;
                        if (!frame.Names!.Add(name) && duplicate is null)
                        {
                            duplicate = JsonPath.Member(frame.Path, name);
                        }

                        break;
                    case JsonTokenType.String:
                        if (FindTextProblem(ref reader) is { } valueProblem)
                        {
                            return Syntax(valueProblem);
                        }

                        Advance(frames);
                        break;
                    default:
                        Advance(frames);
                        break;
                }
            }
        }
        catch (JsonException)
        {
            return Syntax(JsonTextProblemKind.Syntax);
        }

        return duplicate is null ? Maybe.None<JsonTextProblem>() : Maybe.Some(new JsonTextProblem { Kind = JsonTextProblemKind.DuplicateMember, Path = duplicate });
    }

    /// <summary>
    /// 当前的字符串或成员名不是合法 Unicode 的种类
    /// </summary>
    /// <remarks>
    /// <see cref="Utf8JsonReader.Read"/> 不校验字符串内容。先核对原始字节：转义都是 ASCII，不影响 UTF-8 是否合法；字节合法时只有带转义的才可能还原失败，失败的只剩不成对的代理项转义
    /// </remarks>
    /// <param name="reader">停在字符串或成员名上的读取器</param>
    /// <returns>合法时为 <see langword="null"/>；否则是问题的种类</returns>
    private static JsonTextProblemKind? FindTextProblem(ref Utf8JsonReader reader)
    {
        if (!Utf8.IsValid(reader.ValueSpan))
        {
            return JsonTextProblemKind.InvalidUtf8;
        }

        if (!reader.ValueIsEscaped)
        {
            return null;
        }

        try
        {
            _ = reader.GetString();
            return null;
        }
        catch (InvalidOperationException)
        {
            // GetString 对不成对的代理项转义抛出 InvalidOperationException；非法的 UTF-8 已在上面拦下
            return JsonTextProblemKind.LoneSurrogate;
        }
    }

    /// <summary>
    /// 语法错误的结果
    /// </summary>
    /// <remarks>
    /// 各语言的解析器报告语法错误的位置不同，路径一律是 <c>$</c>
    /// </remarks>
    private static Maybe<JsonTextProblem> Syntax(JsonTextProblemKind kind) => Maybe.Some(new JsonTextProblem { Kind = kind, Path = JsonPath.Root });

    /// <summary>
    /// 即将读到的值的路径
    /// </summary>
    /// <remarks>
    /// 已经找到重复成员后路径不再有用，传 <paramref name="needed"/> 为假时省去拼接
    /// </remarks>
    private static string ValuePath(Stack<Frame> frames, bool needed)
    {
        if (!needed || frames.Count == 0)
        {
            return JsonPath.Root;
        }

        var parent = frames.Peek();
        return parent.Names is null ? JsonPath.Index(parent.Path, parent.Index) : JsonPath.Member(parent.Path, parent.PendingName!);
    }

    /// <summary>
    /// 一个值读完：所在的是数组时下标前移
    /// </summary>
    private static void Advance(Stack<Frame> frames)
    {
        if (frames.Count > 0 && frames.Peek() is { Names: null } array)
        {
            array.Index++;
        }
    }

    /// <summary>
    /// 正在读的一个对象或数组
    /// </summary>
    /// <param name="path">它的路径</param>
    /// <param name="names">对象已出现的成员名；数组为 <see langword="null"/></param>
    private sealed class Frame(string path, HashSet<string>? names)
    {
        /// <summary>
        /// 它的路径
        /// </summary>
        public string Path { get; } = path;

        /// <summary>
        /// 对象已出现的成员名
        /// </summary>
        /// <remarks>
        /// 数组为 <see langword="null"/>
        /// </remarks>
        public HashSet<string>? Names { get; } = names;

        /// <summary>
        /// 对象里最近读到的成员名
        /// </summary>
        public string? PendingName { get; set; }

        /// <summary>
        /// 数组里下一项的下标
        /// </summary>
        public int Index { get; set; }
    }
}
