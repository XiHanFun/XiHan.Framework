// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Capabilities;

/// <summary>
/// <c>xihan.manifest.json</c> 与 <see cref="Manifest"/> 的互转。JSON 形态由 <c>spec/manifest/manifest.schema.json</c> 定义；序列化经源生成，不用反射。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class ManifestReader
{
    /// <summary>
    /// 清单文件名。
    /// </summary>
    public const string FileName = "xihan.manifest.json";

    /// <summary>
    /// 从 JSON 文本读取清单。格式或约束错误以 <see cref="KernelErrors.InvalidManifest"/> 返回。
    /// </summary>
    public static Result<Manifest> Read(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        ManifestDocument? document;
        try
        {
            document = JsonSerializer.Deserialize(json, ManifestJsonContext.Default.ManifestDocument);
        }
        catch (JsonException exception)
        {
            return new Error(KernelErrors.InvalidManifest, $"清单不是合法的 JSON：{exception.Message}");
        }

        return document is null
            ? new Error(KernelErrors.InvalidManifest, "清单为空。")
            : Convert(document);
    }

    /// <summary>
    /// 从文件读取清单。
    /// </summary>
    public static Result<Manifest> ReadFile(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!File.Exists(path))
        {
            return new Error(KernelErrors.InvalidManifest, $"清单文件 '{path}' 不存在。");
        }

        return Read(File.ReadAllText(path));
    }

    /// <summary>
    /// 把清单写为 JSON 文本。
    /// </summary>
    public static string Write(Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var document = new ManifestDocument
        {
            Schema = "https://spec.xihanfun.com/manifest/v1.json",
            Id = manifest.Id,
            Package = manifest.Package,
            Version = manifest.Version,
            Primitives = manifest.Primitives.ToString(CultureInfo.InvariantCulture),
            Stability = manifest.Stability.ToString().ToLowerInvariant(),
            Kind = manifest.Kind.ToString().ToLowerInvariant(),
            Role = manifest.Role.ToString().ToLowerInvariant(),
            Isolation = manifest.Isolation switch
            {
                PluginIsolation.InProcess => "in-process",
                PluginIsolation.OutOfProcess => "out-of-process",
                PluginIsolation.Wasm => "wasm",
                _ => null,
            },
            Provides = manifest.Provides.Select(static p => new CapabilityDocument { Id = p.Id, Version = p.Version.ToString(CultureInfo.InvariantCulture) }).ToList(),
            Requires = manifest.Requires.Select(static r => new RequirementDocument { Id = r.Id, Version = r.Version.ToString(CultureInfo.InvariantCulture), Optional = r.Optional }).ToList(),
            Bindings = manifest.Bindings.Select(static b => new BindingDocument { Name = b.Name, Type = b.Type, Required = b.Required }).ToList(),
            Contributes = new ContributionsDocument
            {
                Pipeline = manifest.Contributes.Pipeline.Select(static p => new PipelineDocument { Middleware = p.Middleware, Stage = p.Stage.ToManifestName(), Before = p.Before.ToList(), After = p.After.ToList() }).ToList(),
                Operations = manifest.Contributes.Operations.ToList(),
                Config = manifest.Contributes.Config is null ? null : new ConfigDocument { Section = manifest.Contributes.Config.Section, Schema = manifest.Contributes.Config.Schema },
                Permissions = manifest.Contributes.Permissions.ToList(),
                Health = manifest.Contributes.Health.ToList(),
            },
            Activation = manifest.Activation.ToList(),
            Permissions = manifest.Permissions.ToList(),
            Entry = manifest.Entry,
        };

        return JsonSerializer.Serialize(document, ManifestJsonContext.Default.ManifestDocument);
    }

    private static Result<Manifest> Convert(ManifestDocument document)
    {
        try
        {
            var manifest = new Manifest
            {
                Id = document.Id ?? throw Missing("id"),
                Package = document.Package,
                Version = document.Version ?? throw Missing("version"),
                Primitives = ParseInt(document.Primitives ?? throw Missing("primitives"), "primitives"),
                Stability = ParseEnum<Stability>(document.Stability ?? throw Missing("stability"), "stability"),
                Kind = ParseEnum<ManifestKind>(document.Kind ?? throw Missing("kind"), "kind"),
                Role = document.Role is null ? ManifestRole.Provider : ParseEnum<ManifestRole>(document.Role, "role"),
                Isolation = document.Isolation switch
                {
                    null => null,
                    "in-process" => PluginIsolation.InProcess,
                    "out-of-process" => PluginIsolation.OutOfProcess,
                    "wasm" => PluginIsolation.Wasm,
                    _ => throw new ArgumentException($"isolation 的值 '{document.Isolation}' 不合法。"),
                },
                Provides = (document.Provides ?? []).Select(static p => new CapabilityDeclaration(p.Id ?? throw Missing("provides.id"), ParseInt(p.Version ?? throw Missing("provides.version"), "provides.version"))).ToList(),
                Requires = (document.Requires ?? []).Select(static r => new CapabilityRequirement(r.Id ?? throw Missing("requires.id"), ParseVersion(r.Version ?? throw Missing("requires.version")), r.Optional ?? false)).ToList(),
                Bindings = (document.Bindings ?? []).Select(static b => new Binding(b.Name ?? throw Missing("bindings.name"), b.Type ?? throw Missing("bindings.type"), b.Required ?? true)).ToList(),
                Contributes = document.Contributes is null ? Contributions.Empty : new Contributions
                {
                    Pipeline = (document.Contributes.Pipeline ?? []).Select(static p => new PipelineContribution(
                        p.Middleware ?? throw Missing("contributes.pipeline.middleware"),
                        PipelineStageExtensions.TryParse(p.Stage, out var stage) ? stage : throw new FrameworkException(KernelErrors.UnknownStage, $"未知的管道阶段 '{p.Stage}'。"),
                        p.Before,
                        p.After)).ToList(),
                    Operations = document.Contributes.Operations ?? [],
                    Config = document.Contributes.Config is null ? null : new ConfigContribution(document.Contributes.Config.Section ?? throw Missing("contributes.config.section"), document.Contributes.Config.Schema),
                    Permissions = document.Contributes.Permissions ?? [],
                    Health = document.Contributes.Health ?? [],
                },
                Activation = document.Activation ?? ["onStartup"],
                Permissions = document.Permissions ?? [],
                Entry = document.Entry,
            };

            var validation = manifest.Validate();
            return validation.IsSuccess ? manifest : new Error(KernelErrors.InvalidManifest, validation.Error!.Message);
        }
        catch (ArgumentException exception)
        {
            return new Error(KernelErrors.InvalidManifest, exception.Message);
        }
        catch (FrameworkException exception)
        {
            return new Error(KernelErrors.InvalidManifest, exception.Message);
        }
    }

    private static ArgumentException Missing(string field) => new($"清单缺少必填字段 '{field}'。");

    private static int ParseInt(string text, string field)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
            ? value
            : throw new ArgumentException($"字段 '{field}' 的值 '{text}' 必须是正整数。");

    private static int ParseVersion(string text)
        => ParseInt(text.StartsWith('^') ? text[1..] : text, "requires.version");

    private static TEnum ParseEnum<TEnum>(string text, string field)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : throw new ArgumentException($"字段 '{field}' 的值 '{text}' 不合法。");
}

internal sealed class ManifestDocument
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; set; }

    public string? Id { get; set; }

    public string? Package { get; set; }

    public string? Version { get; set; }

    public string? Primitives { get; set; }

    public string? Stability { get; set; }

    public string? Kind { get; set; }

    public string? Role { get; set; }

    public string? Isolation { get; set; }

    public List<CapabilityDocument>? Provides { get; set; }

    public List<RequirementDocument>? Requires { get; set; }

    public List<BindingDocument>? Bindings { get; set; }

    public ContributionsDocument? Contributes { get; set; }

    public List<string>? Activation { get; set; }

    public List<string>? Permissions { get; set; }

    public string? Entry { get; set; }
}

internal sealed class CapabilityDocument
{
    public string? Id { get; set; }

    public string? Version { get; set; }
}

internal sealed class RequirementDocument
{
    public string? Id { get; set; }

    public string? Version { get; set; }

    public bool? Optional { get; set; }
}

internal sealed class BindingDocument
{
    public string? Name { get; set; }

    public string? Type { get; set; }

    public bool? Required { get; set; }
}

internal sealed class ContributionsDocument
{
    public List<PipelineDocument>? Pipeline { get; set; }

    public List<string>? Operations { get; set; }

    public ConfigDocument? Config { get; set; }

    public List<string>? Permissions { get; set; }

    public List<string>? Health { get; set; }
}

internal sealed class PipelineDocument
{
    public string? Middleware { get; set; }

    public string? Stage { get; set; }

    public List<string>? Before { get; set; }

    public List<string>? After { get; set; }
}

internal sealed class ConfigDocument
{
    public string? Section { get; set; }

    public string? Schema { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true)]
[JsonSerializable(typeof(ManifestDocument))]
internal sealed partial class ManifestJsonContext : JsonSerializerContext;
