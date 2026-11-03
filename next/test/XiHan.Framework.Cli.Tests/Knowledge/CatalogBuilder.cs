// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using XiHan.Framework.Cli.Knowledge;
using XiHan.Framework.Kernel.Hosting;

namespace XiHan.Framework.Cli.Tests.Knowledge;

/// <summary>
/// 从工作区与已加载的程序集生成 <c>catalog.json</c>
/// </summary>
/// <remarks>
/// 包取自 <c>next/src/*/*.csproj</c>（说明、项目引用、包引用与中央包管理的版本）与 <c>next/README.md</c> 的包表；特性取自各包程序集里公开的 <see cref="IFeature"/> 实例的 <c>Manifest</c>
/// </remarks>
internal static partial class CatalogBuilder
{
    /// <summary>
    /// 设为 <c>1</c> 时测试改写索引文件而不是比对
    /// </summary>
    public const string UpdateVariable = "XIHAN_UPDATE_CLI_KNOWLEDGE";

    /// <summary>
    /// 不收进索引的特性：清单由调用方传入，没有固定的清单
    /// </summary>
    public static readonly IReadOnlyList<string> ExcludedFeatures = ["XiHan.Framework.Testing.StubFeature"];

    /// <summary>
    /// 本工具自己的包：它的特性只在 <c>xihan mcp</c> 内嵌应用里装配，不是框架包
    /// </summary>
    private const string CliPackage = "XiHan.Framework.Cli";

    /// <summary>
    /// 索引文件路径
    /// </summary>
    public static string CatalogPath => Path.Combine(TestWorkspace.NextRoot, "src", CliPackage, "Knowledge", "catalog.json");

    /// <summary>
    /// 生成
    /// </summary>
    public static CatalogDocument Build()
    {
        var root = TestWorkspace.NextRoot;
        var versions = XDocument.Load(Path.Combine(root, "Directory.Packages.props"))
            .Descendants("PackageVersion")
            .ToDictionary(static e => (string)e.Attribute("Include")!, static e => (string)e.Attribute("Version")!, StringComparer.Ordinal);
        var table = new Dictionary<string, (string Ring, string Summary)>(StringComparer.Ordinal);
        foreach (var line in File.ReadAllLines(Path.Combine(root, "README.md")))
        {
            var match = PackageRow().Match(line);
            if (match.Success)
            {
                table[match.Groups["id"].Value] = (match.Groups["ring"].Value.Trim(), match.Groups["summary"].Value.Trim());
            }
        }

        var packages = new List<PackageEntry>();
        foreach (var directory in Directory.GetDirectories(Path.Combine(root, "src")).Order(StringComparer.Ordinal))
        {
            var project = Path.Combine(directory, Path.GetFileName(directory) + ".csproj");
            if (!File.Exists(project))
            {
                continue;
            }

            var document = XDocument.Load(project);
            var id = document.Descendants("PackageId").Select(static e => e.Value).FirstOrDefault() ?? Path.GetFileName(directory);
            table.TryGetValue(id, out var row);
            packages.Add(new PackageEntry
            {
                Id = id,
                Ring = row.Ring,
                Summary = row.Summary,
                Description = document.Descendants("Description").Select(static e => e.Value).FirstOrDefault() ?? string.Empty,
                // 分析器形式的项目引用不进运行时依赖
                References = document.Descendants("ProjectReference")
                    .Where(static e => (string?)e.Attribute("OutputItemType") != "Analyzer")
                    .Select(static e => Path.GetFileNameWithoutExtension(((string)e.Attribute("Include")!).Replace('\\', '/')))
                    .ToList(),
                Dependencies = document.Descendants("PackageReference")
                    .Where(static e => !string.Equals((string?)e.Attribute("PrivateAssets"), "all", StringComparison.OrdinalIgnoreCase))
                    .Select(e => (string)e.Attribute("Include")!)
                    .Select(name => new PackageDependency { Id = name, Version = versions.GetValueOrDefault(name, string.Empty) })
                    .ToList(),
                FrameworkReferences = document.Descendants("FrameworkReference").Select(static e => (string)e.Attribute("Include")!).ToList(),
            });
        }

        var features = packages
            .Where(static p => p.Id != CliPackage && !p.Id.EndsWith(".Analyzers", StringComparison.Ordinal) && !p.Id.EndsWith(".Generators", StringComparison.Ordinal))
            .SelectMany(static p => FeaturesOf(p.Id))
            .OrderBy(static f => f.Type, StringComparer.Ordinal)
            .ToList();
        return new CatalogDocument { Packages = packages, Features = features };
    }

    /// <summary>
    /// 一个包里全部公开特性的清单
    /// </summary>
    private static IEnumerable<FeatureEntry> FeaturesOf(string package)
    {
        var assembly = Assembly.Load(new AssemblyName(package));
        foreach (var type in assembly.GetExportedTypes().Where(static t => !t.IsAbstract && !t.IsInterface && typeof(IFeature).IsAssignableFrom(t)))
        {
            if (ExcludedFeatures.Contains(type.FullName))
            {
                continue;
            }

            var manifest = Create(type).Manifest;
            yield return new FeatureEntry
            {
                Type = DisplayName(type),
                Package = package,
                Id = manifest.Id,
                Kind = manifest.Kind.ToString().ToLowerInvariant(),
                Role = manifest.Role.ToString().ToLowerInvariant(),
                Stability = manifest.Stability.ToString().ToLowerInvariant(),
                Provides = manifest.Provides.Select(static p => p.Id + "@" + p.Version).ToList(),
                Requires = manifest.Requires.Select(static r => new RequirementEntry { Id = r.Id, Version = r.Version, Optional = r.Optional }).ToList(),
                Bindings = manifest.Bindings.Select(static b => new BindingEntry { Name = b.Name, Type = b.Type, Required = b.Required }).ToList(),
                Config = manifest.Contributes.Config?.Section,
                Pipeline = manifest.Contributes.Pipeline.Select(static p => new PipelineEntry { Middleware = p.Middleware, Stage = p.Stage.ToString().ToLowerInvariant(), Before = p.Before, After = p.After }).ToList(),
                Operations = manifest.Contributes.Operations,
                Permissions = manifest.Contributes.Permissions,
                Health = manifest.Contributes.Health,
            };
        }
    }

    /// <summary>
    /// 创建特性实例
    /// </summary>
    /// <remarks>
    /// 泛型特性以第一个约束类型封闭；只有一个委托参数的构造函数传空操作的委托（清单与构造参数无关）
    /// </remarks>
    private static IFeature Create(Type type)
    {
        if (type.IsGenericTypeDefinition)
        {
            type = type.MakeGenericType([.. type.GetGenericArguments().Select(static a => a.GetGenericParameterConstraints().FirstOrDefault(static c => c.IsClass) ?? typeof(object))]);
        }

        if (type.GetConstructor(Type.EmptyTypes) is not null)
        {
            return (IFeature)Activator.CreateInstance(type)!;
        }

        var constructor = type.GetConstructors().Single(static c => c.GetParameters() is [{ ParameterType: var p }] && typeof(Delegate).IsAssignableFrom(p));
        var delegateType = constructor.GetParameters()[0].ParameterType;
        var parameters = delegateType.GetMethod("Invoke")!.GetParameters().Select(static p => Expression.Parameter(p.ParameterType)).ToArray();
        var noop = Expression.Lambda(delegateType, Expression.Empty(), parameters).Compile();
        return (IFeature)constructor.Invoke([noop]);
    }

    /// <summary>
    /// 类型的完全限定名，泛型写作 <c>Name&lt;T&gt;</c>
    /// </summary>
    private static string DisplayName(Type type) => type.IsGenericTypeDefinition
        ? type.Namespace + "." + type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)] + "<" + string.Join(", ", type.GetGenericArguments().Select(static a => a.Name)) + ">"
        : type.FullName!;

    /// <summary>
    /// <c>next/README.md</c> 包表的一行：包 ID、环层、内容
    /// </summary>
    [GeneratedRegex(@"^\|\s*`(?<id>XiHan\.Framework\.[A-Za-z0-9.]+)`\s*\|(?<ring>[^|]*)\|(?<summary>.*)\|\s*$")]
    private static partial Regex PackageRow();
}
