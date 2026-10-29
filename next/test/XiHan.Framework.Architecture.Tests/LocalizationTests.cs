// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using XiHan.Framework.Architecture.Tests.Model;
using XiHan.Framework.Localization.Abstractions.Locales;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 本地化约定（RFC-0010）。
/// </summary>
/// <remarks>
/// 带语言文件的包每种内置区域一个文件且键集一致、占位符与 <c>en-US</c> 一致、键的领域来自术语表、文件以约定逻辑名嵌入；<c>&lt;领域&gt;Messages</c> 常量与 <c>en-US.json</c> 一一对应；正式包里的 <see cref="Error"/> 调用点只用消息键常量。
/// </remarks>
public sealed partial class LocalizationTests
{
    /// <summary>
    /// 允许用 <c>Error.Literal</c> 或自由文本的文件：线上还原与异常转换。
    /// </summary>
    private static readonly string[] _literalAllowed =
    [
        "XiHan.Framework.Primitives/FrameworkException.cs",
        "XiHan.Framework.Caching.Abstractions/Idempotency/IdempotencyRecord.cs",
        "XiHan.Framework.Workflow.Abstractions/Instances/WorkflowFault.cs",
        "XiHan.Framework.Scheduling.Abstractions/Jobs/JobRunRecord.cs",
        "XiHan.Framework.Auditing.Database/Trail/AuditRecordMapping.cs",
        "XiHan.Framework.Plugins.Protocol/Mapping/PluginErrorMapper.cs",
    ];

    /// <summary>
    /// 带语言文件的正式包：包 ID → 区域 → 键 → 模板。
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Packages { get; } = LoadPackages();

    [Fact]
    public void EveryLocalizedPackage_HasOneFilePerLocale_WithConsistentKeys()
    {
        var failures = new List<string>();
        var expectedLocales = SupportedLocales.All.OrderBy(static l => l, StringComparer.Ordinal).ToList();
        var domains = Vocabulary.Words.Select(static w => w.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);

        Assert.True(Packages.Count >= 8, $"Only {Packages.Count} packages carry Localization files; the scan looks broken.");

        foreach (var (package, byLocale) in Packages)
        {
            var locales = byLocale.Keys.OrderBy(static l => l, StringComparer.Ordinal).ToList();
            if (!locales.SequenceEqual(expectedLocales, StringComparer.Ordinal))
            {
                failures.Add($"{package}: expected locale files [{string.Join(", ", expectedLocales)}], found [{string.Join(", ", locales)}]");
                continue;
            }

            var source = byLocale[SupportedLocales.Default];
            foreach (var key in source.Keys)
            {
                if (!MessageTemplate.IsValidKey(key))
                {
                    failures.Add($"{package}: key '{key}' is not a valid message key");
                }
                else if (!domains.Contains(MessageTemplate.DomainOf(key)))
                {
                    failures.Add($"{package}: key '{key}' does not start with a vocabulary word");
                }
            }

            var placeholders = source.ToDictionary(static e => e.Key, static e => MessageTemplate.PlaceholdersOf(e.Value).OrderBy(static p => p, StringComparer.Ordinal).ToList(), StringComparer.Ordinal);
            foreach (var (locale, templates) in byLocale)
            {
                var missing = source.Keys.Except(templates.Keys, StringComparer.Ordinal).ToList();
                var extra = templates.Keys.Except(source.Keys, StringComparer.Ordinal).ToList();
                if (missing.Count > 0 || extra.Count > 0)
                {
                    failures.Add($"{package}/{locale}: missing [{string.Join(", ", missing)}], extra [{string.Join(", ", extra)}]");
                    continue;
                }

                foreach (var (key, template) in templates)
                {
                    if (string.IsNullOrWhiteSpace(template))
                    {
                        failures.Add($"{package}/{locale}: template for '{key}' is blank");
                        continue;
                    }

                    var actual = MessageTemplate.PlaceholdersOf(template).OrderBy(static p => p, StringComparer.Ordinal).ToList();
                    if (!actual.SequenceEqual(placeholders[key], StringComparer.Ordinal))
                    {
                        failures.Add($"{package}/{locale}: placeholders of '{key}' [{string.Join(", ", actual)}] differ from en-US [{string.Join(", ", placeholders[key])}]");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void LocaleFiles_AreEmbeddedWithTheConventionalLogicalName()
    {
        var failures = new List<string>();

        foreach (var package in Packages.Keys)
        {
            var project = Workspace.SourceProjects.Single(p => p.PackageId == package);
            var embedded = Workspace.LoadXml(Path.Combine(project.Directory, project.PackageId + ".csproj"))
                .Descendants("EmbeddedResource")
                .Any(static e => string.Equals(e.Attribute("Include")?.Value, "Localization/*.json", StringComparison.Ordinal)
                    && string.Equals(e.Attribute("LogicalName")?.Value, "Localization/%(Filename)%(Extension)", StringComparison.Ordinal));

            if (!embedded)
            {
                failures.Add($"{package}: Localization/*.json must be embedded with LogicalName=\"Localization/%(Filename)%(Extension)\"");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void MessageConstants_MatchTheEnglishLocaleFile()
    {
        var failures = new List<string>();
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var assembly in NamingTests.LoadSourceAssemblies())
        {
            var package = assembly.GetName().Name!;
            var constants = assembly.GetExportedTypes()
                .Where(static t => t.IsAbstract && t.IsSealed && t.Name.EndsWith("Messages", StringComparison.Ordinal))
                .SelectMany(static t => t.GetFields(BindingFlags.Public | BindingFlags.Static).Where(static f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (Type: t, Field: f)))
                .Where(static m => !string.Equals(m.Field.Name, "CatalogName", StringComparison.Ordinal))
                .Select(static m => (m.Type, m.Field, Key: (string)m.Field.GetRawConstantValue()!))
                .ToList();

            if (constants.Count == 0 && !Packages.ContainsKey(package))
            {
                continue;
            }

            if (!Packages.TryGetValue(package, out var byLocale))
            {
                failures.Add($"{package}: defines message constants but has no Localization/<locale>.json files");
                continue;
            }

            foreach (var (type, field, key) in constants)
            {
                if (!MessageTemplate.IsValidKey(key))
                {
                    failures.Add($"{type.FullName}.{field.Name}: '{key}' is not a valid message key");
                }
                else if (seen.TryGetValue(key, out var owner))
                {
                    failures.Add($"{type.FullName}.{field.Name}: '{key}' is also defined by {owner}");
                }
                else
                {
                    seen[key] = $"{type.FullName}.{field.Name}";
                }
            }

            var defined = constants.Select(static c => c.Key).OrderBy(static k => k, StringComparer.Ordinal).ToList();
            var filed = byLocale[SupportedLocales.Default].Keys.OrderBy(static k => k, StringComparer.Ordinal).ToList();
            var missing = filed.Except(defined, StringComparer.Ordinal).ToList();
            var extra = defined.Except(filed, StringComparer.Ordinal).ToList();
            if (missing.Count > 0 || extra.Count > 0)
            {
                failures.Add($"{package}: constants without en-US template [{string.Join(", ", extra)}], en-US templates without constant [{string.Join(", ", missing)}]");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ErrorCallSites_UseMessageKeyConstants()
    {
        var failures = new List<string>();

        foreach (var project in Workspace.SourceProjects.Where(static p => !p.PackageId.EndsWith(".Conformance", StringComparison.Ordinal)))
        {
            foreach (var file in Workspace.SourceFiles(project.Directory))
            {
                var relative = Workspace.Relative(file)["src/".Length..];
                if (Array.IndexOf(_literalAllowed, relative) >= 0)
                {
                    // 线上还原与异常转换用的是存下来的键或文本，不是常量
                    continue;
                }

                var text = File.ReadAllText(file);
                foreach (Match match in ErrorCall().Matches(text))
                {
                    var line = text.AsSpan(0, match.Index).Count('\n') + 1;
                    var arguments = SplitArguments(ReadBalanced(text, match.Index + match.Length));
                    if (match.Value.StartsWith("Error.Literal", StringComparison.Ordinal))
                    {
                        failures.Add($"{relative}:{line}: Error.Literal is only allowed for wire reconstruction and exception conversion");
                        continue;
                    }

                    var message = arguments.Count > 1 ? arguments[1] : string.Empty;
                    if (!MessageConstant().IsMatch(message))
                    {
                        failures.Add($"{relative}:{line}: message argument '{message}' must be a <Domain>Messages constant");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    /// <summary>
    /// 扫描每个正式包目录下的 <c>Localization/*.json</c>，按包 → 区域 → 键 → 模板装载。
    /// </summary>
    private static Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> LoadPackages()
    {
        var packages = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>(StringComparer.Ordinal);
        foreach (var project in Workspace.SourceProjects)
        {
            var directory = Path.Combine(project.Directory, "Localization");
            if (!Directory.Exists(directory))
            {
                continue;
            }

            var byLocale = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
            {
                var locale = Path.GetFileNameWithoutExtension(file);
                byLocale[locale] = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file))
                    ?? throw new InvalidOperationException($"{file} is empty.");
            }

            packages[project.PackageId] = byLocale;
        }

        return packages;
    }

    /// <summary>
    /// 读取从 <paramref name="start"/>（左括号之后）到配对右括号之间的文本，跳过字符串字面量。
    /// </summary>
    private static string ReadBalanced(string text, int start)
    {
        var depth = 1;
        var i = start;
        var inString = false;
        while (i < text.Length && depth > 0)
        {
            var c = text[i];
            if (inString)
            {
                if (c == '\\')
                {
                    i += 2;
                    continue;
                }

                inString = c != '"';
            }
            else if (c == '"')
            {
                inString = true;
            }
            else if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }

            i++;
        }

        return text[start..(i - 1)];
    }

    /// <summary>
    /// 按顶层逗号切分参数列表，忽略字符串、括号与花括号里的逗号。
    /// </summary>
    private static List<string> SplitArguments(string arguments)
    {
        var result = new List<string>();
        var depth = 0;
        var inString = false;
        var current = new System.Text.StringBuilder();
        for (var i = 0; i < arguments.Length; i++)
        {
            var c = arguments[i];
            if (inString)
            {
                current.Append(c);
                if (c == '\\' && i + 1 < arguments.Length)
                {
                    current.Append(arguments[++i]);
                    continue;
                }

                inString = c != '"';
                continue;
            }

            switch (c)
            {
                case '"':
                    inString = true;
                    current.Append(c);
                    break;
                case '(' or '[' or '{':
                    depth++;
                    current.Append(c);
                    break;
                case ')' or ']' or '}':
                    depth--;
                    current.Append(c);
                    break;
                case ',' when depth == 0:
                    result.Add(current.ToString().Trim());
                    current.Clear();
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }

        return result;
    }

    /// <summary>
    /// 错误构造调用：工厂方法、<c>Literal</c> 或构造函数。
    /// </summary>
    [GeneratedRegex(@"(?:Error\.(?:Validation|NotFound|Conflict|Denied|Unavailable|Unexpected|Literal)|new Error)\(")]
    private static partial Regex ErrorCall();

    /// <summary>
    /// 消息键常量引用：<c>&lt;领域&gt;Messages.&lt;名称&gt;</c>。
    /// </summary>
    [GeneratedRegex(@"^\w+Messages\.\w+$")]
    private static partial Regex MessageConstant();
}
