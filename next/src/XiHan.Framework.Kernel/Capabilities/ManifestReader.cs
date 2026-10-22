// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Globalization;
using System.Text.Json;
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
            return new Error(KernelErrors.InvalidManifest, KernelMessages.ManifestInvalidJson, MessageArguments.Of(("reason", exception.Message)));
        }

        // 字面量 "null" 能成功反序列化为 null，同样视为非法清单。
        return document is null
            ? new Error(KernelErrors.InvalidManifest, KernelMessages.ManifestEmpty)
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
            return new Error(KernelErrors.InvalidManifest, KernelMessages.ManifestFileMissing, MessageArguments.Of(("path", path)));
        }

        return Read(File.ReadAllText(path));
    }

    /// <summary>
    /// 把清单写为 JSON 文本。
    /// </summary>
    public static string Write(Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        // 枚举统一写为小写名，与 schema 的枚举值一致；隔离边界的 kebab-case 映射与 Convert 中的读取一一对应。
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

    /// <summary>
    /// 把 JSON 形态转换为 <see cref="Manifest"/>：必填字段缺失、格式错误与跨字段约束失败都收敛为 <see cref="KernelErrors.InvalidManifest"/>。
    /// </summary>
    private static Result<Manifest> Convert(ManifestDocument document)
    {
        // 原语层以 ArgumentException 报告单字段格式错误，阶段名未知以 FrameworkException 报告；
        // 这里把两者都翻译成 Result 失败，调用方不需要区分异常来源。
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
                    _ => throw new ArgumentException($"The value '{document.Isolation}' of field 'isolation' is not valid."),
                },
                Provides = (document.Provides ?? []).Select(static p => new CapabilityDeclaration(p.Id ?? throw Missing("provides.id"), ParseInt(p.Version ?? throw Missing("provides.version"), "provides.version"))).ToList(),
                Requires = (document.Requires ?? []).Select(static r => new CapabilityRequirement(r.Id ?? throw Missing("requires.id"), ParseVersion(r.Version ?? throw Missing("requires.version")), r.Optional ?? false)).ToList(),
                Bindings = (document.Bindings ?? []).Select(static b => new Binding(b.Name ?? throw Missing("bindings.name"), b.Type ?? throw Missing("bindings.type"), b.Required ?? true)).ToList(),
                Contributes = document.Contributes is null ? Contributions.Empty : new Contributions
                {
                    Pipeline = (document.Contributes.Pipeline ?? []).Select(static p => new PipelineContribution(
                        p.Middleware ?? throw Missing("contributes.pipeline.middleware"),
                        PipelineStageExtensions.TryParse(p.Stage, out var stage) ? stage : throw new FrameworkException(KernelErrors.UnknownStage, $"Unknown pipeline stage '{p.Stage}'."),
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
            return validation.IsSuccess ? manifest : validation.Error!.WithCode(KernelErrors.InvalidManifest);
        }
        catch (ArgumentException exception)
        {
            return new Error(KernelErrors.InvalidManifest, KernelMessages.ManifestInvalid, MessageArguments.Of(("reason", exception.Message)));
        }
        catch (FrameworkException exception)
        {
            return new Error(KernelErrors.InvalidManifest, KernelMessages.ManifestInvalid, MessageArguments.Of(("reason", exception.Message)));
        }
    }

    /// <summary>
    /// 构造"必填字段缺失"异常；在对象初始化器里以 <c>?? throw</c> 形式使用。
    /// </summary>
    /// <param name="field">字段的 JSON 路径，例如 <c>provides.id</c>。</param>
    private static ArgumentException Missing(string field) => new($"The manifest is missing the required field '{field}'.");

    /// <summary>
    /// 把字段文本解析为正整数；非数字或小于 1 时抛出 <see cref="ArgumentException"/>。
    /// </summary>
    /// <param name="text">原始文本。</param>
    /// <param name="field">字段的 JSON 路径，用于错误信息。</param>
    private static int ParseInt(string text, string field)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 1
            ? value
            : throw new ArgumentException($"The value '{text}' of field '{field}' must be a positive integer.");

    /// <summary>
    /// 解析 <c>requires.version</c>：允许 <c>^</c> 前缀，语义与不带前缀相同。
    /// </summary>
    private static int ParseVersion(string text)
        => ParseInt(text.StartsWith('^') ? text[1..] : text, "requires.version");

    /// <summary>
    /// 把字段文本解析为枚举，不区分大小写；未定义的值抛出 <see cref="ArgumentException"/>。
    /// </summary>
    /// <typeparam name="TEnum">枚举类型。</typeparam>
    /// <param name="text">原始文本。</param>
    /// <param name="field">字段的 JSON 路径，用于错误信息。</param>
    private static TEnum ParseEnum<TEnum>(string text, string field)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(text, ignoreCase: true, out var value) && Enum.IsDefined(value)
            ? value
            : throw new ArgumentException($"The value '{text}' of field '{field}' is not valid.");
}
