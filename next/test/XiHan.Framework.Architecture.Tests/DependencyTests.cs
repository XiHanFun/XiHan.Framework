// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 依赖门禁：只能依赖更低的环（同环只经契约包），Ring 0/1 与抽象包零第三方依赖，公开签名不泄漏第三方类型。
/// </summary>
public sealed class DependencyTests
{
    private static readonly string[] AllowedExternalPackages = ["Microsoft.Extensions.Options", "Microsoft.Extensions.Primitives"];

    private static readonly string[] AllowedSignatureAssemblyPrefixes = ["System", "netstandard", "mscorlib", "Microsoft.Extensions.", Workspace.PackagePrefix];

    [Fact]
    public void ProjectReferences_PointToLowerRingsOrSameRingAbstractions()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.SourceProjects)
        {
            var sourceRing = PackageGrammar.Ring(project.PackageId);
            foreach (var target in project.ProjectReferences)
            {
                var targetRing = PackageGrammar.Ring(target);
                var ok = sourceRing is 6 or 7
                    || targetRing < sourceRing
                    || (targetRing == sourceRing && PackageGrammar.IsContract(target));

                if (!ok)
                {
                    failures.Add($"{project.PackageId}（Ring {sourceRing}）不得引用 {target}（Ring {targetRing}）");
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

            foreach (var reference in project.PackageReferences.Where(static r => !r.IsPrivate))
            {
                var allowed = (reference.Id.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
                        && reference.Id.EndsWith(".Abstractions", StringComparison.Ordinal))
                    || Array.IndexOf(AllowedExternalPackages, reference.Id) >= 0;

                if (!allowed)
                {
                    failures.Add($"{project.PackageId} 不得引用 {reference.Id}：Ring 0/1 与抽象包只允许 Microsoft.Extensions.* 抽象包");
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
                        if (!AllowedSignatureAssemblyPrefixes.Any(prefix => owner.StartsWith(prefix, StringComparison.Ordinal)))
                        {
                            failures.Add($"{type.FullName}.{member.Name} 的签名暴露了第三方类型 {referenced.FullName}（{owner}）");
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

        Assert.True(missing.Count == 0, "架构测试项目必须引用全部正式包，缺少：" + string.Join("、", missing));
    }

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
