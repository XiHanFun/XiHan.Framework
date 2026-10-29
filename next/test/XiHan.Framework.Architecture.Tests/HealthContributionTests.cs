// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 健康项约定（ADR-0030）。
/// </summary>
/// <remarks>
/// 清单 <c>contributes.health</c> 声明的每一项都必须由同一个包实现——名字作为 <c>ContributorStatus(</c> 的条目名或 <c>PeriodicHealthContributor(</c> 的第一个参数出现（字面量或同包常量），且包里有实现 <c>IHealthContributor</c> 的类型或注册了 <c>IHealthContributor</c>；反过来，注册了 <c>IHealthContributor</c> 的包其清单必须声明 <c>Health</c>。静态扫描源码，不实例化特性。
/// </remarks>
public sealed partial class HealthContributionTests
{
    /// <summary>
    /// 条目名在运行时才确定、清单无法枚举的包。
    /// </summary>
    /// <remarks>
    /// 插件宿主（条目名由插件自己的清单声明，RFC-0017 / RFC-0018 的 <c>contributes.health</c>）与命名存储实例的注册表（每个实例一条 <c>objectstorage.store.&lt;name&gt;</c>，ADR-0050）。只豁免反向检查。
    /// </remarks>
    private static readonly string[] _dynamicContributors = ["XiHan.Framework.Plugins.OutOfProcess", "XiHan.Framework.Plugins.Wasm", "XiHan.Framework.ObjectStorage.Abstractions"];

    /// <summary>
    /// 每个正式包的扫描结果。
    /// </summary>
    private static IReadOnlyList<PackageHealth> Packages { get; } = Workspace.SourceProjects.Select(Scan).ToList();

    [Fact]
    public void DeclaredHealthItems_AreImplementedInTheSamePackage()
    {
        var failures = new List<string>();
        var declaring = Packages.Where(static p => p.Declared.Count > 0).ToList();
        Assert.True(declaring.Count >= 14, $"Only {declaring.Count} packages declare health items; the scan looks broken.");

        foreach (var package in declaring)
        {
            if (!package.Implements && !package.Registers)
            {
                failures.Add($"{package.Id}: declares health items [{string.Join(", ", package.Declared)}] but neither implements nor registers IHealthContributor");
            }

            foreach (var name in package.Declared.Where(name => !package.Implemented.Contains(name)))
            {
                failures.Add($"{package.Id}: health item '{name}' is declared in the manifest but no ContributorStatus(...) or PeriodicHealthContributor(...) in the package carries that name");
            }

            foreach (var expression in package.Unresolved)
            {
                failures.Add($"{package.Id}: health item '{expression}' in the manifest is neither a string literal nor a const string of this package");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void PackagesRegisteringContributors_DeclareHealthItems()
    {
        var failures = Packages
            .Where(static p => p.Registers && p.Declared.Count == 0 && Array.IndexOf(_dynamicContributors, p.Id) < 0)
            .Select(static p => $"{p.Id}: registers IHealthContributor but its manifest declares no Contributes.Health item")
            .ToList();

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 扫描一个包。
    /// </summary>
    /// <remarks>
    /// 清单声明、实现处出现的条目名、常量表、是否实现或注册 <c>IHealthContributor</c>。
    /// </remarks>
    private static PackageHealth Scan(SourceProject project)
    {
        var texts = Workspace.SourceFiles(project.Directory).Select(File.ReadAllText).ToList();
        var constants = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (Match match in texts.SelectMany(static text => Constant().Matches(text)))
        {
            var name = match.Groups["name"].Value;
            var value = match.Groups["value"].Value;
            // 同名常量值不同即歧义：引用它的条目名按无法解析处理
            constants[name] = constants.TryGetValue(name, out var existing) && !string.Equals(existing, value, StringComparison.Ordinal) ? null : value;
        }

        var declared = new HashSet<string>(StringComparer.Ordinal);
        var unresolved = new List<string>();
        foreach (var item in texts.SelectMany(static text => Declaration().Matches(text)).SelectMany(static m => SplitItems(m.Groups["items"].Value)))
        {
            if (Resolve(item, constants) is { } name)
            {
                declared.Add(name);
            }
            else
            {
                unresolved.Add(item);
            }
        }

        // 实现处的名字表达式解析不出来（如插件宿主的 HealthPrefix + id、内核自己的参数名）就跳过：它们不对应清单里的静态条目
        var implemented = texts
            .SelectMany(static text => Implementation().Matches(text))
            .Select(m => Resolve(m.Groups["name"].Value, constants))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        return new PackageHealth(
            project.PackageId,
            declared,
            unresolved,
            implemented,
            texts.Any(static text => Implementer().IsMatch(text)),
            texts.Any(static text => Registration().IsMatch(text)));
    }

    /// <summary>
    /// 把条目名表达式解析成字面量。
    /// </summary>
    /// <remarks>
    /// 带引号的取引号内文本，标识符取同包常量（<c>XxxFeature.HealthName</c> 取最后一段）。
    /// </remarks>
    private static string? Resolve(string expression, Dictionary<string, string?> constants)
    {
        var trimmed = expression.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return trimmed[1..^1];
        }

        if (!Identifier().IsMatch(trimmed))
        {
            return null;
        }

        var name = trimmed[(trimmed.LastIndexOf('.') + 1)..];
        return constants.TryGetValue(name, out var value) ? value : null;
    }

    /// <summary>
    /// 按逗号切分集合表达式里的项，忽略空白与尾随逗号。
    /// </summary>
    private static IEnumerable<string> SplitItems(string items)
        => items.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(static item => item.Length > 0);

    /// <summary>
    /// 清单里的 <c>Health = [ ... ]</c> 声明。
    /// </summary>
    [GeneratedRegex(@"\bHealth\s*=\s*\[(?<items>[^\]]*)\]", RegexOptions.Singleline)]
    private static partial Regex Declaration();

    /// <summary>
    /// 实现处：<c>ContributorStatus(</c> 的条目名或 <c>PeriodicHealthContributor(</c> 的第一个参数。
    /// </summary>
    [GeneratedRegex(@"\b(?:ContributorStatus|PeriodicHealthContributor)\(\s*(?<name>[^,\)]+)")]
    private static partial Regex Implementation();

    /// <summary>
    /// <c>const string</c> 常量声明。
    /// </summary>
    [GeneratedRegex(@"\bconst\s+string\s+(?<name>\w+)\s*=\s*""(?<value>[^""]+)""")]
    private static partial Regex Constant();

    /// <summary>
    /// 实现 <c>IHealthContributor</c> 的类型声明（含主构造函数写法）。
    /// </summary>
    [GeneratedRegex(@"\bclass\s+\w+(?:<[^>]+>)?\s*(?:\([^)]*\))?\s*:\s*[^{]*\bIHealthContributor\b")]
    private static partial Regex Implementer();

    /// <summary>
    /// 向容器注册 <c>IHealthContributor</c> 的调用。
    /// </summary>
    /// <remarks>
    /// <c>AddSingleton</c> / <c>TryAddSingleton</c> 及带键变体。内核采集用的 <c>GetServices</c> 不算。
    /// </remarks>
    [GeneratedRegex(@"\b(?:Try)?Add(?:Keyed)?(?:Singleton|Scoped|Transient)<IHealthContributor>\s*\(")]
    private static partial Regex Registration();

    /// <summary>
    /// 简单或带限定的标识符。
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*$")]
    private static partial Regex Identifier();

    /// <summary>
    /// 一个包的健康项事实。
    /// </summary>
    /// <param name="Id">包 ID。</param>
    /// <param name="Declared">清单声明的条目名（已解析成字面量）。</param>
    /// <param name="Unresolved">清单里解析不出来的条目表达式。</param>
    /// <param name="Implemented">实现处出现的条目名。</param>
    /// <param name="Implements">是否有实现 <c>IHealthContributor</c> 的类型。</param>
    /// <param name="Registers">是否注册了 <c>IHealthContributor</c>。</param>
    private sealed record PackageHealth(string Id, IReadOnlySet<string> Declared, IReadOnlyList<string> Unresolved, IReadOnlySet<string> Implemented, bool Implements, bool Registers);
}
