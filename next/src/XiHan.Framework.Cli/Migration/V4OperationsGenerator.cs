// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-06 生成的源码：操作类（带请求记录）、每项目一个 <c>EmptyResponse</c> 与 <c>&lt;项目末段&gt;OperationsJsonContext</c>。
/// </summary>
internal static class V4OperationsGenerator
{
    /// <summary>
    /// 生成代码总要引入的命名空间。
    /// </summary>
    private static readonly string[] _usings = ["XiHan.Framework.Kernel", "XiHan.Framework.Kernel.Operations", "XiHan.Framework.Primitives"];

    /// <summary>
    /// 操作类文件：<c>[Operation(id, Permission / Policy)]</c>，主构造函数注入原服务，<c>HandleAsync</c> 只做委托；需要时同文件附请求记录。
    /// </summary>
    public static string Operation(V4OperationProject project, string resourcePascal, IEnumerable<string> usings, string operationId, string? permission, string? policy, string className, string serviceType, string requestType, string responseType, string handler, string? requestRecord)
    {
        var newLine = project.NewLine;
        var builder = new StringBuilder();
        builder.Append("// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine).Append(newLine);
        foreach (var ns in usings.Concat(_usings).Append(project.Name + ".Operations").Distinct(StringComparer.Ordinal).OrderBy(static ns => ns, StringComparer.Ordinal))
        {
            builder.Append("using ").Append(ns).Append(';').Append(newLine);
        }

        builder.Append(newLine).Append("namespace ").Append(project.Name).Append(".Operations.").Append(resourcePascal).Append(';').Append(newLine).Append(newLine);
        AppendSummary(builder, string.Empty, newLine);
        builder.Append("[Operation(\"").Append(operationId).Append('"');
        if (permission is not null)
        {
            builder.Append(", Permission = ").Append(permission);
        }
        else if (policy is not null)
        {
            builder.Append(", Policy = \"").Append(policy).Append('"');
        }

        builder.Append(")]").Append(newLine);
        builder.Append("public sealed class ").Append(className).Append('(').Append(serviceType).Append(" service) : IOperation<").Append(requestType).Append(", ").Append(responseType).Append('>').Append(newLine);
        builder.Append('{').Append(newLine);
        builder.Append("    /// <inheritdoc />").Append(newLine);
        builder.Append(handler);
        builder.Append('}').Append(newLine);
        if (requestRecord is not null)
        {
            builder.Append(newLine);
            AppendSummary(builder, string.Empty, newLine);
            builder.Append(requestRecord).Append(newLine);
        }

        return builder.ToString();
    }

    /// <summary>
    /// 把项目级文件（空响应、序列化上下文）加进会话的生成文件，并记报告；第一行是提醒登记的标记。
    /// </summary>
    public static void Complete(MigrationSession session, string root)
    {
        foreach (var (projectDirectory, project) in session.OperationProjects.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            var newLine = project.NewLine;
            var operations = Path.Combine(projectDirectory, "Operations");
            if (project.NeedsEmptyResponse)
            {
                var path = Path.Combine(operations, "EmptyResponse.cs");
                session.GeneratedFiles[path] = EmptyResponse(project);
                session.Changed("V4-06", Describe(path, root, projectDirectory, newLine), 0);
            }

            var contextPath = Path.Combine(operations, Stem(project.Name) + "OperationsJsonContext.cs");
            var message = CliMessages.MigrateMarkerAddOperations.L();
            var contextFile = Describe(contextPath, root, projectDirectory, newLine);
            session.GeneratedFiles[contextPath] = JsonContext(project, Markers.Line("V4-06", message));
            session.Marked("V4-06", contextFile, 1, message);
            session.Changed("V4-06", contextFile, 0);
        }
    }

    /// <summary>
    /// 项目名末段。
    /// </summary>
    public static string Stem(string project) => project.Contains('.', StringComparison.Ordinal) ? project[(project.LastIndexOf('.') + 1)..] : project;

    /// <summary>
    /// <c>EmptyResponse</c>：没有返回值的方法的响应类型。
    /// </summary>
    private static string EmptyResponse(V4OperationProject project)
    {
        var newLine = project.NewLine;
        var builder = new StringBuilder();
        builder.Append("// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine).Append(newLine);
        builder.Append("namespace ").Append(project.Name).Append(".Operations;").Append(newLine).Append(newLine);
        AppendSummary(builder, string.Empty, newLine);
        builder.Append("public sealed record EmptyResponse").Append(newLine).Append('{').Append(newLine);
        AppendSummary(builder, "    ", newLine);
        builder.Append("    public static EmptyResponse Instance { get; } = new();").Append(newLine);
        builder.Append('}').Append(newLine);
        return builder.ToString();
    }

    /// <summary>
    /// 序列化上下文：camelCase、不区分大小写、允许从字符串读数字，登记全部请求与响应类型；生成的类型写全限定名，简单名重复的加 <c>TypeInfoPropertyName</c>。
    /// </summary>
    private static string JsonContext(V4OperationProject project, string marker)
    {
        var newLine = project.NewLine;
        var builder = new StringBuilder();
        builder.Append(marker).Append(newLine);
        builder.Append("// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine).Append(newLine);
        foreach (var ns in project.Usings.Append("System.Text.Json.Serialization").Distinct(StringComparer.Ordinal).OrderBy(static ns => ns, StringComparer.Ordinal))
        {
            builder.Append("using ").Append(ns).Append(';').Append(newLine);
        }

        builder.Append(newLine).Append("namespace ").Append(project.Name).Append(".Operations;").Append(newLine).Append(newLine);
        AppendSummary(builder, string.Empty, newLine);
        builder.Append("[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, NumberHandling = JsonNumberHandling.AllowReadingFromString)]").Append(newLine);
        var operands = project.Types.Select(TypeOfOperand).Distinct(StringComparer.Ordinal).OrderBy(static type => type, StringComparer.Ordinal).ToList();
        // 不同资源各自生成的同名请求记录（DeleteRequest …）：源生成上下文的属性名按简单名推导会撞车，重名的显式给属性名
        var duplicates = operands.GroupBy(SimpleNameOf, StringComparer.Ordinal).Where(static group => group.Count() > 1).Select(static group => group.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var type in operands)
        {
            builder.Append("[JsonSerializable(typeof(").Append(type).Append(')');
            var simple = SimpleNameOf(type);
            if (duplicates.Contains(simple) && ResourceSegmentOf(type) is { } resource && simple.All(static c => char.IsLetterOrDigit(c) || c == '_'))
            {
                builder.Append(", TypeInfoPropertyName = \"").Append(resource).Append(simple).Append('"');
            }

            builder.Append(")]").Append(newLine);
        }

        builder.Append("internal sealed partial class ").Append(Stem(project.Name)).Append("OperationsJsonContext : JsonSerializerContext;").Append(newLine);
        return builder.ToString();
    }

    /// <summary>
    /// 可空值类型（<c>typeof(int?)</c> 合法，<c>typeof(Dto?)</c> 不合法）。
    /// </summary>
    private static readonly HashSet<string> _valueTypes = new(StringComparer.Ordinal)
    {
        "bool", "byte", "sbyte", "char", "short", "ushort", "int", "uint", "long", "ulong", "float", "double", "decimal", "nint", "nuint",
        "Guid", "DateTime", "DateTimeOffset", "TimeSpan", "DateOnly", "TimeOnly", "Int32", "Int64", "Boolean", "Decimal", "Double", "Single",
    };

    /// <summary>
    /// <c>typeof</c> 的操作数：引用类型去掉末尾的可空标注（语法级分不清值类型，只对常见值类型保留 <c>?</c>）。
    /// </summary>
    public static string TypeOfOperand(string type)
    {
        var trimmed = type.Trim();
        if (!trimmed.EndsWith('?'))
        {
            return trimmed;
        }

        var inner = trimmed[..^1].Trim();
        return _valueTypes.Contains(inner) || inner.StartsWith("System.", StringComparison.Ordinal) && _valueTypes.Contains(inner["System.".Length..]) ? trimmed : inner;
    }

    /// <summary>
    /// 类型源码去掉命名空间前缀后的简单名（泛型实参原样保留）。
    /// </summary>
    private static string SimpleNameOf(string type)
    {
        var head = type.Split('<')[0];
        var lastDot = head.LastIndexOf('.');
        return lastDot < 0 ? type : type[(lastDot + 1)..];
    }

    /// <summary>
    /// 全限定名的最后一个命名空间段（<c>Shop.Operations.Order.DeleteRequest</c> → <c>Order</c>）；没有命名空间前缀为空。
    /// </summary>
    private static string? ResourceSegmentOf(string type)
    {
        var head = type.Split('<')[0];
        var lastDot = head.LastIndexOf('.');
        if (lastDot <= 0)
        {
            return null;
        }

        var ns = head[..lastDot];
        var previous = ns.LastIndexOf('.');
        return previous < 0 ? ns : ns[(previous + 1)..];
    }

    /// <summary>
    /// 三行 summary，内容是「由工具生成」。
    /// </summary>
    private static void AppendSummary(StringBuilder builder, string indent, string newLine)
    {
        builder.Append(indent).Append("/// <summary>").Append(newLine);
        builder.Append(indent).Append("/// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine);
        builder.Append(indent).Append("/// </summary>").Append(newLine);
    }

    /// <summary>
    /// 生成文件的描述，报告里用相对根的路径。
    /// </summary>
    private static MigrationFile Describe(string path, string root, string projectDirectory, string newLine)
        => new(path, Path.GetRelativePath(root, path).Replace('\\', '/'), projectDirectory, newLine);
}
