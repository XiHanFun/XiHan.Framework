// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 依赖门禁。
/// </summary>
/// <remarks>
/// 只能依赖更低的环（同环只经契约包），Ring 0/1 与抽象包零第三方依赖，公开签名不泄漏第三方类型。
/// </remarks>
public sealed class DependencyTests
{
    /// <summary>
    /// Ring 1/2 除 <c>Microsoft.Extensions.*.Abstractions</c> 外额外允许的包。
    /// </summary>
    /// <remarks>
    /// 前两个没有 Abstractions 拆分但本身只含契约与原语；<c>System.Threading.RateLimiting</c> 是 BCL 的限流原语（不在共享框架里、没有其他依赖），内核 Resilience 阶段用它（ADR-0029）。
    /// </remarks>
    private static readonly string[] _allowedExternalPackages = ["Microsoft.Extensions.Options", "Microsoft.Extensions.Primitives", "System.Threading.RateLimiting"];

    /// <summary>
    /// 公开签名允许引用的程序集名前缀：BCL、Microsoft.Extensions 与框架自身。
    /// </summary>
    private static readonly string[] _allowedSignatureAssemblyPrefixes = ["System", "netstandard", "mscorlib", "Microsoft.Extensions.", Workspace.PackagePrefix];

    [Fact]
    public void ProjectReferences_PointToLowerRingsOrSameRingAbstractions()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.SourceProjects)
        {
            var sourceRing = PackageGrammar.Ring(project.PackageId);
            foreach (var target in project.ProjectReferences)
            {
                // Profiles（6）与工具链（7）可以引用任何环；其余只能向下，同环必须经契约包。
                var targetRing = PackageGrammar.Ring(target);
                var ok = sourceRing is 6 or 7
                    || targetRing < sourceRing
                    || (targetRing == sourceRing && PackageGrammar.IsContract(target));

                if (!ok)
                {
                    failures.Add($"'{project.PackageId}' (ring {sourceRing}) must not reference '{target}' (ring {targetRing}).");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Primitives_HasNoPackageOrProjectReferences()
    {
        var primitives = Workspace.SourceProjects.Single(static p => p.PackageId == "XiHan.Framework.Primitives");

        Assert.Empty(primitives.ProjectReferences);
        Assert.DoesNotContain(primitives.PackageReferences, static r => !r.IsPrivate);
    }

    [Fact]
    public void KernelAndAbstractions_ReferenceOnlyMicrosoftExtensionsAbstractions()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.SourceProjects)
        {
            var ring = PackageGrammar.Ring(project.PackageId);
            if (ring is not (1 or 2))
            {
                continue;
            }

            // PrivateAssets=all 的引用（分析器、构建工具）不进入包依赖，不受此约束。
            foreach (var reference in project.PackageReferences.Where(static r => !r.IsPrivate))
            {
                var allowed = (reference.Id.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
                        && reference.Id.EndsWith(".Abstractions", StringComparison.Ordinal))
                    || Array.IndexOf(_allowedExternalPackages, reference.Id) >= 0;

                if (!allowed)
                {
                    failures.Add($"'{project.PackageId}' must not reference '{reference.Id}': ring 0/1 and abstraction packages may only reference Microsoft.Extensions.* abstraction packages or the allow-listed packages ({string.Join(", ", _allowedExternalPackages)}).");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PublicSignatures_DoNotLeakThirdPartyTypes()
    {
        var failures = new List<string>();

        // 只约束 Ring 0–2：宿主、协议与 Provider 的公开面天然包含其所适配的技术类型
        foreach (var assembly in NamingTests.LoadSourceAssemblies().Where(static a => PackageGrammar.Ring(a.GetName().Name ?? string.Empty) <= 2))
        {
            foreach (var type in assembly.GetExportedTypes())
            {
                foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    foreach (var referenced in ReferencedTypes(member))
                    {
                        var owner = referenced.Assembly.GetName().Name ?? string.Empty;
                        if (!_allowedSignatureAssemblyPrefixes.Any(prefix => owner.StartsWith(prefix, StringComparison.Ordinal)))
                        {
                            failures.Add($"The signature of '{type.FullName}.{member.Name}' exposes third-party type '{referenced.FullName}' ({owner}).");
                        }
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EverySourceProject_IsReferencedByArchitectureTests()
    {
        var self = SourceProject.Load(Path.Combine(Workspace.TestRoot, "XiHan.Framework.Architecture.Tests", "XiHan.Framework.Architecture.Tests.csproj"));
        var missing = Workspace.SourceProjects
            .Select(static p => p.PackageId)
            .Where(id => !id.EndsWith(".Analyzers", StringComparison.Ordinal) && !id.EndsWith(".Generators", StringComparison.Ordinal))
            .Where(id => !self.ProjectReferences.Contains(id, StringComparer.Ordinal))
            .ToList();

        Assert.True(missing.Count == 0, "The architecture test project must reference every source package; missing: " + string.Join(", ", missing) + ".");
    }

    /// <summary>
    /// 枚举成员公开签名里直接出现的类型。
    /// </summary>
    /// <remarks>
    /// 返回类型、参数类型、属性与字段类型、事件处理器类型。
    /// </remarks>
    private static IEnumerable<Type> ReferencedTypes(MemberInfo member)
    {
        switch (member)
        {
            case MethodInfo method:
                yield return method.ReturnType;
                foreach (var parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }

                break;
            case PropertyInfo property:
                yield return property.PropertyType;
                break;
            case FieldInfo field:
                yield return field.FieldType;
                break;
            case ConstructorInfo constructor:
                foreach (var parameter in constructor.GetParameters())
                {
                    yield return parameter.ParameterType;
                }

                break;
            case EventInfo @event when @event.EventHandlerType is not null:
                yield return @event.EventHandlerType;
                break;
        }
    }
}
