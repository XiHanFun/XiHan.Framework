// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 把 V4-03 收集的注册项写成每个项目一个 <c>&lt;项目末段&gt;V4Services.cs</c>：<c>Add&lt;项目末段&gt;V4Services(this IServiceCollection)</c> 按原生存期显式注册。
/// </summary>
internal static class V4ServicesGenerator
{
    /// <summary>
    /// 项目目录对应的生成类名：取目录里字典序第一个 <c>.csproj</c> 的末段加 <c>V4Services</c>。
    /// </summary>
    public static (string Project, string ClassName) Names(string projectDirectory)
    {
        var project = Directory.EnumerateFiles(projectDirectory, "*.csproj").Select(Path.GetFileNameWithoutExtension).OrderBy(static name => name, StringComparer.Ordinal).First()!;
        var stem = project.Contains('.', StringComparison.Ordinal) ? project[(project.LastIndexOf('.') + 1)..] : project;
        return (project, stem + "V4Services");
    }

    /// <summary>
    /// 生成文件内容；第一行是提醒调用的标记。
    /// </summary>
    public static string Generate(string project, string className, IReadOnlyList<V4ServiceRegistration> registrations, string marker, string newLine)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        var builder = new StringBuilder();
        builder.Append(marker).Append(newLine);
        builder.Append("// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine).Append(newLine);
        builder.Append("using Microsoft.Extensions.DependencyInjection;").Append(newLine);
        // 实现类所在的命名空间按字典序引入；与项目命名空间相同的也引入，生成的文件不依赖它在哪个目录；有消息处理器时引入 AddMessageHandler 所在的命名空间
        var namespaces = registrations.Select(static registration => registration.Namespace).Where(static ns => ns.Length > 0);
        if (registrations.Any(static registration => registration.Topics is { Count: > 0 }))
        {
            namespaces = namespaces.Append("XiHan.Framework.Messaging.Abstractions");
        }

        foreach (var ns in namespaces.Distinct(StringComparer.Ordinal).OrderBy(static ns => ns, StringComparer.Ordinal))
        {
            builder.Append("using ").Append(ns).Append(';').Append(newLine);
        }

        builder.Append(newLine).Append("namespace ").Append(project).Append(';').Append(newLine).Append(newLine);
        builder.Append("/// <summary>").Append(newLine).Append("/// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine).Append("/// </summary>").Append(newLine);
        builder.Append("public static class ").Append(className).Append(newLine).Append('{').Append(newLine);
        builder.Append("    /// <summary>").Append(newLine).Append("    /// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine).Append("    /// </summary>").Append(newLine);
        builder.Append("    public static IServiceCollection Add").Append(className).Append("(this IServiceCollection services)").Append(newLine).Append("    {").Append(newLine);
        foreach (var registration in registrations.OrderBy(static registration => registration.Namespace, StringComparer.Ordinal).ThenBy(static registration => registration.Implementation, StringComparer.Ordinal))
        {
            // 消息处理器：按主题登记（AddMessageHandler 自己按作用域注册处理器）
            if (registration.Topics is { Count: > 0 })
            {
                foreach (var topic in registration.Topics)
                {
                    builder.Append("        services.AddMessageHandler<").Append(registration.Implementation).Append(">(").Append(topic).Append(");").Append(newLine);
                }

                continue;
            }

            if (registration.Services.Count == 0)
            {
                builder.Append("        services.Add").Append(registration.Lifetime).Append('<').Append(registration.Implementation).Append(">();").Append(newLine);
                continue;
            }

            foreach (var service in registration.Services)
            {
                builder.Append("        services.Add").Append(registration.Lifetime).Append('<').Append(service).Append(", ").Append(registration.Implementation).Append(">();").Append(newLine);
            }
        }

        builder.Append("        return services;").Append(newLine).Append("    }").Append(newLine).Append('}').Append(newLine);
        return builder.ToString();
    }
}
