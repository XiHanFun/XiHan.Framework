// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.Unicode;

namespace XiHan.Framework.Kernel.Json;

/// <summary>
/// XiHan 读取 JSON 文本的共同检查
/// </summary>
/// <remarks>
/// 操作载荷与清单在交给反序列化之前都先过这一遍（RFC-0001「JSON 文本」，ADR-0236）：语法按 RFC 8259（不允许注释、尾逗号、BOM 与多个根值），嵌套不超过 64 层，字符串与成员名必须是合法的 Unicode（没有非法 UTF-8，没有孤立的代理项转义），任何一层都不能有重复的成员名。
/// 语法错误不论出现在哪里都优先于重复成员，所以遇到重复成员也读到末尾；各语言的解析器报告语法错误的位置不同，这里统一只给 <c>$</c>
/// </remarks>
internal static class JsonText
{
    /// <summary>
    /// 最大嵌套层数
    /// </summary>
    public const int MaxDepth = 64;

    /// <summary>
    /// 检查 UTF-8 JSON 文本
    /// </summary>
    /// <returns>通过时为 <see langword="null"/>；否则是第一个问题</returns>
    public static JsonTextProblem? Check(ReadOnlySpan<byte> utf8)
    {
        var reader = new Utf8JsonReader(utf8, new JsonReaderOptions { MaxDepth = MaxDepth });
        var frames = new Stack<Frame>();
        string? duplicate = null;
        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        frames.Push(new Frame(ValuePath(frames, duplicate is null), new HashSet<string>(StringComparer.Ordinal)));
                        break;
                    case JsonTokenType.StartArray:
                        frames.Push(new Frame(ValuePath(frames, duplicate is null), null));
                        break;
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                        frames.Pop();
                        Advance(frames);
                        break;
                    case JsonTokenType.PropertyName:
                        // GetString 对非法 UTF-8 与孤立代理项的转义抛出 InvalidOperationException
                        var name = reader.GetString()!;
                        var frame = frames.Peek();
                        frame.PendingName = name;
                        if (!frame.Names!.Add(name) && duplicate is null)
                        {
                            duplicate = JsonPath.Member(frame.Path, name);
                        }

                        break;
                    case JsonTokenType.String:
                        // Read 不校验字符串内容：有转义时取一次字符串（孤立代理项在这里失败），没有转义时只核对 UTF-8
                        if (reader.ValueIsEscaped)
                        {
                            _ = reader.GetString();
                        }
                        else if (!Utf8.IsValid(reader.ValueSpan))
                        {
                            return new JsonTextProblem(true, JsonPath.Root);
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
            return new JsonTextProblem(true, JsonPath.Root);
        }
        catch (InvalidOperationException)
        {
            return new JsonTextProblem(true, JsonPath.Root);
        }

        return duplicate is null ? null : new JsonTextProblem(false, duplicate);
    }

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
