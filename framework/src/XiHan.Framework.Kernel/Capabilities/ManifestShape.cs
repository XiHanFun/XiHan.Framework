// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using XiHan.Framework.Kernel.Json;
using XiHan.Framework.Kernel.Security;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// 清单 JSON 的形状检查
/// </summary>
/// <remarks>
/// 照 <c>manifest.schema.json</c> 逐项核对（RFC-0002 解析规则 2，ADR-0236）：按成员在文本里出现的顺序检查，每个成员依次是未知成员、<c>null</c>、类型、取值，对象与数组进到里面再查；一个对象的成员查完后按 Schema 的 <c>required</c> 顺序查缺少的成员。
/// 第一处问题的路径就是错误的 <c>reason</c>，各语言照同一顺序报出同一个路径。跨字段的约束（插件必须声明隔离、能力不得重复等）不在这里，由 <see cref="Manifest.Validate"/> 在之后核对
/// </remarks>
internal static partial class ManifestShape
{
    /// <summary>
    /// 根对象的形状
    /// </summary>
    private static readonly ObjectShape _root = BuildRoot();

    /// <summary>
    /// 找出清单 JSON 不合 Schema 的第一处
    /// </summary>
    /// <returns>该处的路径；合 Schema 时为 <see langword="null"/></returns>
    public static string? FindProblem(JsonElement root)
        => root.ValueKind == JsonValueKind.Object ? CheckObject(root, _root, JsonPath.Root) : JsonPath.Root;

    /// <summary>
    /// 检查一个对象
    /// </summary>
    private static string? CheckObject(JsonElement element, ObjectShape shape, string path)
    {
        foreach (var property in element.EnumerateObject())
        {
            var memberPath = JsonPath.Member(path, property.Name);
            if (!shape.Members.TryGetValue(property.Name, out var member))
            {
                return memberPath;
            }

            if (CheckValue(property.Value, member, memberPath) is { } problem)
            {
                return problem;
            }
        }

        // 缺少的成员在对象的成员都查完之后才查，与按对象反序列化的实现在对象结束时才发现缺少成员一致（各语言同一顺序）
        foreach (var required in shape.Required)
        {
            if (!element.TryGetProperty(required, out _))
            {
                return JsonPath.Member(path, required);
            }
        }

        return null;
    }

    /// <summary>
    /// 检查一个值
    /// </summary>
    /// <remarks>
    /// Schema 里没有哪个成员或数组项能取 <c>null</c>：可选成员缺失表示没有，写成 <c>null</c> 不合法
    /// </remarks>
    private static string? CheckValue(JsonElement value, ValueShape shape, string path)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return path;
        }

        switch (shape.Kind)
        {
            case ShapeKind.String:
                return value.ValueKind == JsonValueKind.String && (shape.Accepts is null || shape.Accepts(value.GetString()!)) ? null : path;
            case ShapeKind.Boolean:
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : path;
            case ShapeKind.Object:
                return value.ValueKind == JsonValueKind.Object ? CheckObject(value, shape.Object!, path) : path;
            default:
                if (value.ValueKind != JsonValueKind.Array || (shape.NonEmpty && value.GetArrayLength() == 0))
                {
                    return path;
                }

                // 不得重复的字符串数组：先核对这一项本身，再看它是否与前面的项重复，重复时报第二次出现的位置
                var seen = shape.Unique ? new HashSet<string>(StringComparer.Ordinal) : null;
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    var itemPath = JsonPath.Index(path, index++);
                    if (CheckValue(item, shape.Item!, itemPath) is { } problem)
                    {
                        return problem;
                    }

                    if (seen is not null && !seen.Add(item.GetString()!))
                    {
                        return itemPath;
                    }
                }

                return null;
        }
    }

    /// <summary>
    /// 按 <c>manifest.schema.json</c> 建出根对象的形状
    /// </summary>
    private static ObjectShape BuildRoot()
    {
        var any = new ValueShape(ShapeKind.String);
        var flag = new ValueShape(ShapeKind.Boolean);
        var capability = new ObjectShape(
            new Dictionary<string, ValueShape>(StringComparer.Ordinal)
            {
                ["id"] = Text(CapabilityId.IsValid),
                ["version"] = Text(IsPositiveInt32),
            },
            ["id", "version"]);
        var requirement = new ObjectShape(
            new Dictionary<string, ValueShape>(StringComparer.Ordinal)
            {
                ["id"] = Text(CapabilityId.IsValid),
                ["version"] = Text(static text => IsPositiveInt32(text.StartsWith('^') ? text[1..] : text)),
                ["optional"] = flag,
            },
            ["id", "version"]);
        var binding = new ObjectShape(
            new Dictionary<string, ValueShape>(StringComparer.Ordinal)
            {
                ["name"] = Text(static text => BindingNamePattern().IsMatch(text)),
                ["type"] = Text(static text => BindingTypePattern().IsMatch(text)),
                ["required"] = flag,
            },
            ["name", "type"]);
        var pipeline = new ObjectShape(
            new Dictionary<string, ValueShape>(StringComparer.Ordinal)
            {
                ["middleware"] = Text(static text => text.Length > 0),
                ["stage"] = Text(static text => PipelineStageExtensions.TryParse(text, out _)),
                ["before"] = List(any),
                ["after"] = List(any),
            },
            ["middleware", "stage"]);
        var config = new ObjectShape(
            new Dictionary<string, ValueShape>(StringComparer.Ordinal)
            {
                ["section"] = Text(static text => ConfigSectionPattern().IsMatch(text)),
                ["schema"] = any,
            },
            ["section"]);
        var contributions = new ObjectShape(
            new Dictionary<string, ValueShape>(StringComparer.Ordinal)
            {
                ["pipeline"] = List(new ValueShape(ShapeKind.Object) { Object = pipeline }),
                ["operations"] = List(Text(OperationId.IsValid)),
                ["config"] = new ValueShape(ShapeKind.Object) { Object = config },
                ["permissions"] = List(Text(PermissionDefinition.IsValidCode)) with { Unique = true },
                ["health"] = List(Text(static text => HealthPattern().IsMatch(text))),
            },
            []);
        return new ObjectShape(
            new Dictionary<string, ValueShape>(StringComparer.Ordinal)
            {
                ["$schema"] = any,
                ["id"] = Text(ManifestId.IsValid),
                ["package"] = any,
                ["version"] = Text(static text => SemVerPattern().IsMatch(text)),
                ["primitives"] = Text(IsPositiveInt32),
                ["stability"] = Text(static text => text is "stable" or "preview" or "experimental" or "internal" or "deprecated"),
                ["kind"] = Text(static text => text is "capability" or "provider" or "plugin" or "host" or "protocol"),
                ["role"] = Text(static text => text is "provider" or "default"),
                ["isolation"] = Text(static text => text is "in-process" or "wasm" or "out-of-process"),
                ["provides"] = List(new ValueShape(ShapeKind.Object) { Object = capability }),
                ["requires"] = List(new ValueShape(ShapeKind.Object) { Object = requirement }),
                ["bindings"] = List(new ValueShape(ShapeKind.Object) { Object = binding }),
                ["contributes"] = new ValueShape(ShapeKind.Object) { Object = contributions },
                ["activation"] = List(Text(ActivationEvent.IsValid)) with { NonEmpty = true },
                ["permissions"] = List(Text(static text => PermissionPattern().IsMatch(text))),
                ["entry"] = any,
            },
            ["id", "version", "primitives", "stability", "kind"]);
    }

    /// <summary>
    /// 取值受约束的字符串
    /// </summary>
    private static ValueShape Text(Func<string, bool> accepts) => new(ShapeKind.String) { Accepts = accepts };

    /// <summary>
    /// 数组
    /// </summary>
    private static ValueShape List(ValueShape item) => new(ShapeKind.Array) { Item = item };

    /// <summary>
    /// 是否是 32 位范围内的正整数文本
    /// </summary>
    /// <remarks>
    /// Schema 的 <c>^[1-9][0-9]*$</c> 不限位数，读出的版本号是 32 位有符号整数，超过 2147483647 同样不合法
    /// </remarks>
    private static bool IsPositiveInt32(string text)
        => PositivePattern().IsMatch(text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    /// <summary>
    /// 正整数文本
    /// </summary>
    [GeneratedRegex("^[1-9][0-9]*\\z")]
    private static partial Regex PositivePattern();

    /// <summary>
    /// SemVer 2.0 版本号（manifest.schema.json 的 <c>$defs/semver</c>）
    /// </summary>
    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(-[0-9A-Za-z.-]+)?(\\+[0-9A-Za-z.-]+)?\\z")]
    private static partial Regex SemVerPattern();

    /// <summary>
    /// 绑定名
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9-]*\\z")]
    private static partial Regex BindingNamePattern();

    /// <summary>
    /// 绑定类型
    /// </summary>
    [GeneratedRegex("^[a-z]+(:[a-z]+)?\\z")]
    private static partial Regex BindingTypePattern();

    /// <summary>
    /// 配置节
    /// </summary>
    [GeneratedRegex("^XiHan:[A-Z][A-Za-z0-9]*(:[A-Z][A-Za-z0-9]*)*\\z")]
    private static partial Regex ConfigSectionPattern();

    /// <summary>
    /// 健康检查名
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9.-]*\\z")]
    private static partial Regex HealthPattern();

    /// <summary>
    /// 插件申请的权限
    /// </summary>
    /// <remarks>
    /// <c>.+</c> 不跨换行：.NET 的 <c>.</c> 不匹配 <c>\n</c>，与 ECMAScript 一致
    /// </remarks>
    [GeneratedRegex("^(net\\.outbound|fs\\.read|fs\\.write|capability):.+\\z")]
    private static partial Regex PermissionPattern();

    /// <summary>
    /// 值的种类
    /// </summary>
    private enum ShapeKind
    {
        /// <summary>
        /// 字符串
        /// </summary>
        String,

        /// <summary>
        /// 布尔
        /// </summary>
        Boolean,

        /// <summary>
        /// 对象
        /// </summary>
        Object,

        /// <summary>
        /// 数组
        /// </summary>
        Array,
    }

    /// <summary>
    /// 一个对象的形状
    /// </summary>
    /// <param name="Members">允许的成员（区分大小写）</param>
    /// <param name="Required">必填成员，按 Schema 的顺序</param>
    private sealed record ObjectShape(Dictionary<string, ValueShape> Members, string[] Required);

    /// <summary>
    /// 一个值的形状
    /// </summary>
    /// <param name="Kind">种类</param>
    private sealed record ValueShape(ShapeKind Kind)
    {
        /// <summary>
        /// 字符串的取值约束
        /// </summary>
        public Func<string, bool>? Accepts { get; init; }

        /// <summary>
        /// 对象的形状
        /// </summary>
        public ObjectShape? Object { get; init; }

        /// <summary>
        /// 数组项的形状
        /// </summary>
        public ValueShape? Item { get; init; }

        /// <summary>
        /// 数组至少一项
        /// </summary>
        public bool NonEmpty { get; init; }

        /// <summary>
        /// 字符串数组的项不得重复
        /// </summary>
        public bool Unique { get; init; }
    }
}
