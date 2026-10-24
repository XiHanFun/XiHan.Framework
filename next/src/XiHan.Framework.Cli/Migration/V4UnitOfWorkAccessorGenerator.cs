// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-05 生成的源码：给单例消费方用的 <c>ICurrentUnitOfWorkAccessor</c>（只读当前工作单元）与从当前调用的服务作用域读 <c>IUnitOfWorkManager.Current</c> 的实现。
/// </summary>
internal static class V4UnitOfWorkAccessorGenerator
{
    /// <summary>
    /// 生成的两个文件名（在项目的 <c>Data</c> 目录下）。
    /// </summary>
    public static readonly string[] FileNames = ["ICurrentUnitOfWorkAccessor.cs", "CurrentUnitOfWorkAccessor.cs"];

    /// <summary>
    /// 生成文件名 → 内容。
    /// </summary>
    /// <param name="ns">生成的命名空间（<c>&lt;项目&gt;.Data</c>）。</param>
    /// <param name="managerNamespace">应用承接的 <c>IUnitOfWorkManager</c> 所在命名空间；不在迁移范围内为 <see langword="null"/>。</param>
    /// <param name="marker">第一行的标记（说明 <c>IUnitOfWorkManager</c> / <c>IUnitOfWork</c> 要指向应用自己的类型）。</param>
    /// <param name="newLine">换行符。</param>
    public static IReadOnlyDictionary<string, string> Generate(string ns, string? managerNamespace, string marker, string newLine)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ICurrentUnitOfWorkAccessor.cs"] = Interface(ns, managerNamespace, marker, newLine),
            ["CurrentUnitOfWorkAccessor.cs"] = Implementation(ns, managerNamespace, marker, newLine),
        };

    /// <summary>
    /// 文件头、标记与 using。
    /// </summary>
    private static StringBuilder Header(string ns, string marker, string newLine, IEnumerable<string> usings)
    {
        var builder = new StringBuilder();
        builder.Append(marker).Append(newLine);
        builder.Append("// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine).Append(newLine);
        var any = false;
        foreach (var directive in usings.Distinct(StringComparer.Ordinal).OrderBy(static directive => directive, StringComparer.Ordinal))
        {
            builder.Append("using ").Append(directive).Append(';').Append(newLine);
            any = true;
        }

        if (any)
        {
            builder.Append(newLine);
        }

        builder.Append("namespace ").Append(ns).Append(';').Append(newLine).Append(newLine);
        return builder;
    }

    /// <summary>
    /// 三行 summary，内容是「由工具生成」。
    /// </summary>
    private static void Summary(StringBuilder builder, string indent, string newLine)
    {
        builder.Append(indent).Append("/// <summary>").Append(newLine);
        builder.Append(indent).Append("/// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine);
        builder.Append(indent).Append("/// </summary>").Append(newLine);
    }

    /// <summary>
    /// <c>ICurrentUnitOfWorkAccessor</c>：<c>IUnitOfWork? Current</c>，没有显式开启的工作单元时为空。
    /// </summary>
    private static string Interface(string ns, string? managerNamespace, string marker, string newLine)
    {
        var builder = Header(ns, marker, newLine, managerNamespace is null || managerNamespace == ns ? [] : [managerNamespace]);
        Summary(builder, string.Empty, newLine);
        builder.Append("public interface ICurrentUnitOfWorkAccessor").Append(newLine).Append('{').Append(newLine);
        Summary(builder, "    ", newLine);
        builder.Append("    IUnitOfWork? Current { get; }").Append(newLine);
        builder.Append('}').Append(newLine);
        return builder.ToString();
    }

    /// <summary>
    /// <c>CurrentUnitOfWorkAccessor</c>：可注册为单例；从当前调用的服务作用域解析应用的（作用域、AsyncLocal 维护当前值的）<c>IUnitOfWorkManager</c> 并读 <c>Current</c>，没有当前调用时为空。
    /// </summary>
    private static string Implementation(string ns, string? managerNamespace, string marker, string newLine)
    {
        var usings = new List<string> { "Microsoft.Extensions.DependencyInjection", "XiHan.Framework.Kernel.Pipeline" };
        if (managerNamespace is not null && managerNamespace != ns)
        {
            usings.Add(managerNamespace);
        }

        var builder = Header(ns, marker, newLine, usings);
        Summary(builder, string.Empty, newLine);
        builder.Append("public sealed class CurrentUnitOfWorkAccessor(IInvocationAccessor accessor) : ICurrentUnitOfWorkAccessor").Append(newLine).Append('{').Append(newLine);
        builder.Append("    /// <inheritdoc />").Append(newLine);
        builder.Append("    public IUnitOfWork? Current => accessor.Current.TryGetValue(out var context) ? context.Services.GetService<IUnitOfWorkManager>()?.Current : null;").Append(newLine);
        builder.Append('}').Append(newLine);
        return builder.ToString();
    }
}
