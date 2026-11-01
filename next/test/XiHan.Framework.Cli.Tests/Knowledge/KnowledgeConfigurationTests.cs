// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Configuration;
using XiHan.Framework.Cli.Knowledge;

namespace XiHan.Framework.Cli.Tests.Knowledge;

/// <summary>
/// 配置节索引与各 Options 类的 <c>FromConfiguration</c> 一致。
/// </summary>
/// <remarks>
/// 以记录读取的配置调用每个 Options 类：键都被读到、节内读到的路径都在索引里、必填键缺了就失败、缺省值与不写等价、上下限与枚举取值按索引接受或拒绝。
/// </remarks>
public sealed class KnowledgeConfigurationTests
{
    /// <summary>
    /// 占位符的缺省示例。
    /// </summary>
    private const string DefaultPlaceholder = "sample";

    /// <summary>
    /// 知识库。
    /// </summary>
    private static readonly Lazy<FrameworkKnowledge> _knowledge = new(FrameworkKnowledge.Load);

    /// <summary>
    /// 全部配置节。
    /// </summary>
    public static TheoryData<string> Sections => new(_knowledge.Value.Sections.Select(static s => s.Section));

    [Theory]
    [MemberData(nameof(Sections))]
    public void Section_MatchesWhatItsOptionsRead(string section)
    {
        var entry = _knowledge.Value.Sections.Single(s => s.Section == section);
        var failures = new List<string>();
        var reader = Reader(entry.Options);
        var keys = entry.Keys.Select(k => (Entry: k, Path: PathOf(entry, k))).ToList();
        foreach (var (key, _) in keys.Where(static k => k.Entry.Default is null && k.Entry.Sample is null))
        {
            failures.Add($"'{key.Key}' needs a default or a sample.");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        var full = keys.ToDictionary(static k => k.Path, static k => k.Entry.Default ?? k.Entry.Sample, StringComparer.OrdinalIgnoreCase);
        var required = keys.Where(static k => k.Entry.Required).ToDictionary(static k => k.Path, static k => k.Entry.Sample ?? k.Entry.Default, StringComparer.OrdinalIgnoreCase);

        // 全量配置：每个键都被读到，节内读到的路径都在索引里
        var log = new ReadLog();
        var baseline = Invoke(reader, full, log, failures, "all keys set");
        if (baseline is not null)
        {
            foreach (var (key, path) in keys.Where(k => !log.Reads.Contains(k.Path)))
            {
                failures.Add($"'{key.Key}' ({path}) is indexed but never read.");
            }

            var regions = Regions(entry);
            var nested = _knowledge.Value.Sections.Where(s => s != entry && s.Section.StartsWith(entry.Section + ":", StringComparison.OrdinalIgnoreCase)).Select(static s => s.Section + ":").ToList();
            foreach (var read in log.Reads.Order(StringComparer.Ordinal))
            {
                var inRegion = regions.Any(r => read.StartsWith(r + ":", StringComparison.OrdinalIgnoreCase));
                var inNested = nested.Any(n => read.StartsWith(n, StringComparison.OrdinalIgnoreCase));
                var documented = keys.Any(k => string.Equals(k.Path, read, StringComparison.OrdinalIgnoreCase) || k.Path.StartsWith(read + ":", StringComparison.OrdinalIgnoreCase));
                if (inRegion && !inNested && !documented)
                {
                    failures.Add($"'{read}' is read but not indexed.");
                }
            }
        }

        // 只有必填键：读取成功；去掉任何一个必填键：读取失败
        Invoke(reader, required, null, failures, "only required keys");
        foreach (var (key, path) in keys.Where(static k => k.Entry.Required))
        {
            ExpectFailure(reader, Without(required, path), failures, $"'{key.Key}' is marked required, but reading without it succeeded.");
        }

        if (baseline is not null)
        {
            var snapshot = OptionsSnapshot.Of(baseline);
            foreach (var (key, path) in keys.Where(static k => k.Entry.Default is not null))
            {
                // 缺省值与不写等价：去掉这个键（全量里它取的是缺省值）得到的 Options 必须相同
                var without = Invoke(reader, Without(full, path), null, failures, $"'{key.Key}' removed");
                if (without is not null && OptionsSnapshot.Of(without) != snapshot)
                {
                    failures.Add($"'{key.Key}' default '{key.Default}' differs from leaving the key out.");
                }
            }

            foreach (var (key, path) in keys)
            {
                CheckRange(reader, full, key, path, failures);
            }
        }

        Assert.True(failures.Count == 0, $"{entry.Section} ({entry.Options}):{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    }

    [Fact]
    public void EveryOptionsClassWithFromConfiguration_IsIndexed()
    {
        var indexed = _knowledge.Value.Sections.Select(static s => s.Options).ToHashSet(StringComparer.Ordinal);
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var package in _knowledge.Value.Packages.Where(static p => p.Id != "XiHan.Framework.Cli" && !p.Id.EndsWith(".Analyzers", StringComparison.Ordinal) && !p.Id.EndsWith(".Generators", StringComparison.Ordinal)))
        {
            foreach (var type in Assembly.Load(new AssemblyName(package.Id)).GetExportedTypes())
            {
                var section = type.GetField("ConfigurationSection", BindingFlags.Public | BindingFlags.Static);
                var reader = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Any(static m => m.Name == "FromConfiguration" && m.GetParameters() is [{ ParameterType: var p }, ..] && p == typeof(IConfiguration));
                if (section is { IsLiteral: true } && reader)
                {
                    found.Add(type.FullName!);
                }
            }
        }

        Assert.Equal(found, indexed.Order(StringComparer.Ordinal));
        foreach (var entry in _knowledge.Value.Sections)
        {
            var type = Reader(entry.Options).DeclaringType!;
            Assert.Equal(entry.Section, (string)type.GetField("ConfigurationSection")!.GetRawConstantValue()!);
            Assert.Equal(entry.Package, type.Assembly.GetName().Name);
        }
    }

    /// <summary>
    /// 节与绝对键的固定前缀：读到它们下面的路径都要在索引里。
    /// </summary>
    private static List<string> Regions(ConfigSectionEntry entry)
    {
        var regions = new List<string> { entry.Section };
        foreach (var key in entry.Keys.Where(static k => k.Key.StartsWith("XiHan:", StringComparison.Ordinal)))
        {
            var segments = key.Key.Split(':').TakeWhile(static s => !s.StartsWith('{')).ToList();
            regions.Add(string.Join(':', segments.Take(segments.Count - 1)));
        }

        return regions;
    }

    /// <summary>
    /// 键的完整路径，占位符换成示例。
    /// </summary>
    /// <remarks>
    /// 绝对键原样，其余接在节后。
    /// </remarks>
    private static string PathOf(ConfigSectionEntry section, ConfigKeyEntry key)
    {
        var path = key.Key.StartsWith("XiHan:", StringComparison.Ordinal) ? key.Key : section.Section + ":" + key.Key;
        return string.Join(':', path.Split(':').Select(s => s.StartsWith('{') && s.EndsWith('}')
            ? section.Placeholders?.GetValueOrDefault(s[1..^1]) ?? DefaultPlaceholder
            : s));
    }

    /// <summary>
    /// 上下限与枚举取值。
    /// </summary>
    private static void CheckRange(MethodInfo reader, Dictionary<string, string?> full, ConfigKeyEntry key, string path, List<string> failures)
    {
        static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
        if (key.Minimum is { } minimum)
        {
            Invoke(reader, With(full, path, Number(minimum)), null, failures, $"'{key.Key}' at its minimum {Number(minimum)}");
            ExpectFailure(reader, With(full, path, Number(minimum - 1)), failures, $"'{key.Key}' accepted {Number(minimum - 1)} below its minimum.");
        }

        if (key.Maximum is { } maximum)
        {
            Invoke(reader, With(full, path, Number(maximum)), null, failures, $"'{key.Key}' at its maximum {Number(maximum)}");
            ExpectFailure(reader, With(full, path, Number(maximum + 1)), failures, $"'{key.Key}' accepted {Number(maximum + 1)} above its maximum.");
        }

        if (key.ExclusiveMinimum is { } exclusive)
        {
            ExpectFailure(reader, With(full, path, Number(exclusive)), failures, $"'{key.Key}' accepted {Number(exclusive)} at its exclusive minimum.");
            var inside = key.Type == "number" && key.Maximum is { } upper ? (exclusive + upper) / 2 : exclusive + 1;
            Invoke(reader, With(full, path, Number(inside)), null, failures, $"'{key.Key}' just above its exclusive minimum ({Number(inside)})");
        }

        if (key.Values is { } values)
        {
            foreach (var value in values)
            {
                Invoke(reader, With(full, path, value), null, failures, $"'{key.Key}' = '{value}'");
            }

            ExpectFailure(reader, With(full, path, "__invalid__"), failures, $"'{key.Key}' accepted a value outside {string.Join("/", values)}.");
        }

        if (key.Type == "boolean")
        {
            Invoke(reader, With(full, path, "true"), null, failures, $"'{key.Key}' = true");
            Invoke(reader, With(full, path, "false"), null, failures, $"'{key.Key}' = false");
        }
    }

    /// <summary>
    /// 找 Options 类的 <c>FromConfiguration(IConfiguration, …)</c>。
    /// </summary>
    private static MethodInfo Reader(string options)
    {
        var package = _knowledge.Value.Sections.Single(s => s.Options == options).Package;
        var type = Assembly.Load(new AssemblyName(package)).GetType(options, throwOnError: true)!;
        return type.GetMethods(BindingFlags.Public | BindingFlags.Static).Single(static m => m.Name == "FromConfiguration" && m.GetParameters() is [{ ParameterType: var p }, ..] && p == typeof(IConfiguration));
    }

    /// <summary>
    /// 以给定的键值调用，失败时记下原因并返回空。
    /// </summary>
    private static object? Invoke(MethodInfo reader, IReadOnlyDictionary<string, string?> values, ReadLog? log, List<string> failures, string label)
    {
        try
        {
            return Call(reader, values, log);
        }
        catch (Exception exception) when (exception is not Xunit.Sdk.XunitException)
        {
            failures.Add($"{label}: reading failed with {exception.GetType().Name}: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// 以给定的键值调用，期望失败。
    /// </summary>
    private static void ExpectFailure(MethodInfo reader, IReadOnlyDictionary<string, string?> values, List<string> failures, string message)
    {
        try
        {
            Call(reader, values, null);
            failures.Add(message);
        }
        catch (Exception exception) when (exception is not Xunit.Sdk.XunitException)
        {
            // 期望之中：任何异常都算拒绝
        }
    }

    /// <summary>
    /// 调用：第一个参数是配置，其余（绑定等）取缺省。
    /// </summary>
    private static object Call(MethodInfo reader, IReadOnlyDictionary<string, string?> values, ReadLog? log)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        if (log is not null)
        {
            configuration = new RecordingConfiguration(configuration, log);
        }

        var arguments = reader.GetParameters().Select((p, i) => i == 0 ? configuration : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
        try
        {
            return reader.Invoke(null, arguments)!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Throw(exception.InnerException);
            throw;
        }
    }

    /// <summary>
    /// 复制并改一个键。
    /// </summary>
    private static Dictionary<string, string?> With(Dictionary<string, string?> values, string path, string value) => new(values, StringComparer.OrdinalIgnoreCase) { [path] = value };

    /// <summary>
    /// 复制并去掉一个键。
    /// </summary>
    private static Dictionary<string, string?> Without(Dictionary<string, string?> values, string path)
    {
        var copy = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
        copy.Remove(path);
        return copy;
    }
}
