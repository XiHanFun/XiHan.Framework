// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-06 的命名规则（<c>spec/migration/v4.md</c> §4 V4-06）：资源名、动词、请求类型判定与标识符拆词。
/// </summary>
internal static partial class V4Operations
{
    /// <summary>
    /// 服务类名后缀，长的在前。
    /// </summary>
    private static readonly string[] _serviceSuffixes = ["ApplicationService", "AppService", "Service"];

    /// <summary>
    /// 可直接作为请求类型的参数类型后缀。
    /// </summary>
    private static readonly string[] _requestSuffixes = ["Dto", "Request", "Input"];

    /// <summary>
    /// 资源名词干：类名去掉服务后缀。
    /// </summary>
    public static string ResourceStem(string className)
    {
        foreach (var suffix in _serviceSuffixes)
        {
            if (className.Length > suffix.Length && className.EndsWith(suffix, StringComparison.Ordinal))
            {
                return className[..^suffix.Length];
            }
        }

        return className;
    }

    /// <summary>
    /// 参数类型是否按请求类型直接使用。
    /// </summary>
    public static bool IsRequestType(string typeName) => _requestSuffixes.Any(suffix => typeName.EndsWith(suffix, StringComparison.Ordinal));

    /// <summary>
    /// 把 PascalCase 标识符拆成小写单词（连续大写视为缩写：<c>HTMLParser</c> → <c>html</c>、<c>parser</c>）。
    /// </summary>
    public static List<string> Words(string identifier)
        => WordPattern().Matches(identifier.TrimStart('@', '_')).Select(static match => match.Value.ToLowerInvariant()).ToList();

    /// <summary>
    /// 单词用 <c>-</c> 连接。
    /// </summary>
    public static string Kebab(IEnumerable<string> words) => string.Join('-', words);

    /// <summary>
    /// 单词连成 PascalCase。
    /// </summary>
    public static string Pascal(IEnumerable<string> words) => string.Concat(words.Select(static word => char.ToUpperInvariant(word[0]) + word[1..]));

    /// <summary>
    /// 参数名转属性名：首字母大写，去掉 <c>@</c>。
    /// </summary>
    public static string PropertyName(string parameterName)
    {
        var name = parameterName.TrimStart('@');
        return name.Length == 0 ? "Value" : char.ToUpperInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// 方法名 → 动词：去掉 <c>Async</c> 与资源词，再按首词映射（Create / Add / Insert → create，Update / Modify → update，Delete / Remove → delete，
    /// Get…List / Page / All → list，Query / Search / List / Find → list，Get → get，其余原样），剩余词接在后面。
    /// </summary>
    public static string Verb(string methodName, IReadOnlyList<string> resourceWords)
    {
        var name = methodName.EndsWith("Async", StringComparison.Ordinal) && methodName.Length > "Async".Length ? methodName[..^"Async".Length] : methodName;
        var words = Words(name);
        RemoveResource(words, resourceWords);
        if (words.Count == 0)
        {
            return "invoke";
        }

        var head = words[0];
        var rest = words.Skip(1).ToList();
        switch (head)
        {
            case "create" or "add" or "insert":
                return Join("create", rest);
            case "update" or "modify":
                return Join("update", rest);
            case "delete" or "remove":
                return Join("delete", rest);
            case "query" or "search" or "list" or "find":
                return Join("list", rest);
            case "get":
                // Get…List / Page / All 是列表；其余的 Get 是读取
                if (rest.Count > 0 && rest[^1] is "list" or "page" or "paged" or "all")
                {
                    return Join("list", rest[..^1]);
                }

                return Join("get", rest);
            default:
                return Kebab(words);
        }
    }

    /// <summary>
    /// 动词后接剩余词。
    /// </summary>
    private static string Join(string verb, List<string> rest) => rest.Count == 0 ? verb : verb + "-" + Kebab(rest);

    /// <summary>
    /// 去掉首词之后第一次出现的资源词序列（<c>UpdateAccessPolicyStatus</c> 的 <c>access policy</c>）。
    /// </summary>
    private static void RemoveResource(List<string> words, IReadOnlyList<string> resource)
    {
        if (resource.Count == 0)
        {
            return;
        }

        for (var start = 1; start + resource.Count <= words.Count; start++)
        {
            var matches = true;
            for (var offset = 0; offset < resource.Count; offset++)
            {
                if (!string.Equals(words[start + offset], resource[offset], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                words.RemoveRange(start, resource.Count);
                return;
            }
        }
    }

    /// <summary>
    /// 单词：连续大写（后面不是小写）或可选大写开头的小写与数字串。
    /// </summary>
    [GeneratedRegex("[A-Z]+(?![a-z])|[A-Z]?[a-z0-9]+")]
    private static partial Regex WordPattern();
}
