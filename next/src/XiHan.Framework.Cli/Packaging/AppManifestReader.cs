// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.Json;
using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 读取并校验应用清单；全部问题一次列出。
/// </summary>
internal static partial class AppManifestReader
{
    /// <summary>
    /// 默认文件名。
    /// </summary>
    public const string DefaultFileName = "xihan.app.json";

    /// <summary>
    /// 从文件读取；文件不存在由调用方先判断。
    /// </summary>
    public static Result<AppManifest> Read(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        AppManifest? manifest;
        try
        {
            using var stream = File.OpenRead(path);
            manifest = JsonSerializer.Deserialize(stream, AppManifestJsonContext.Default.AppManifest);
        }
        catch (JsonException exception)
        {
            return Invalid(path, exception.Message);
        }

        if (manifest is null)
        {
            return Invalid(path, "the document is null");
        }

        var problems = Validate(manifest, Path.GetDirectoryName(Path.GetFullPath(path))!);
        return problems.Count == 0 ? manifest : Invalid(path, string.Join("; ", problems));
    }

    /// <summary>
    /// 校验，返回全部问题（空表示合法）。
    /// </summary>
    public static IReadOnlyList<string> Validate(AppManifest manifest, string manifestDirectory)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(manifestDirectory);
        var problems = new List<string>();
        if (!DnsLabel().IsMatch(manifest.Name))
        {
            problems.Add($"name '{manifest.Name}' must be a DNS label (^[a-z][a-z0-9-]{{0,62}}$ not ending with '-')");
        }

        if (string.IsNullOrWhiteSpace(manifest.Project))
        {
            problems.Add("project is required");
        }
        else if (!File.Exists(Path.Combine(manifestDirectory, manifest.Project)))
        {
            problems.Add($"project '{manifest.Project}' does not exist next to the manifest");
        }

        if (string.IsNullOrWhiteSpace(manifest.Build.Context))
        {
            problems.Add("build.context must not be blank");
        }
        else if (!Directory.Exists(Path.Combine(manifestDirectory, manifest.Build.Context)))
        {
            problems.Add($"build.context '{manifest.Build.Context}' does not exist relative to the manifest");
        }

        if (manifest.Profile is not ("minimal" or "standard" or "full"))
        {
            problems.Add($"profile '{manifest.Profile}' must be minimal, standard or full");
        }

        if (manifest.Image.Repository is { } repository && string.IsNullOrWhiteSpace(repository))
        {
            problems.Add("image.repository must not be blank");
        }

        if (string.IsNullOrWhiteSpace(manifest.Image.Tag))
        {
            problems.Add("image.tag must not be blank");
        }

        if (manifest.Ports.Http is < 1 or > 65535)
        {
            problems.Add("ports.http must be between 1 and 65535");
        }

        if (manifest.Ports.Grpc is { } grpc && (grpc is < 1 or > 65535 || grpc == manifest.Ports.Http))
        {
            problems.Add("ports.grpc must be between 1 and 65535 and differ from ports.http");
        }

        foreach (var (name, binding) in manifest.Bindings)
        {
            if (!BindingName().IsMatch(name))
            {
                problems.Add($"binding name '{name}' must match ^[a-z][a-z0-9-]*$");
            }

            if (BindingKind.Find(binding.Type) is null)
            {
                problems.Add($"binding '{name}' has unknown type '{binding.Type}'; expected {string.Join(", ", BindingKind.All.Select(static k => k.Type))}");
            }

            if (binding.Secret is { } secret && !DnsLabel().IsMatch(secret))
            {
                problems.Add($"binding '{name}' secret '{secret}' must be a DNS label");
            }
        }

        if (!manifest.Config.MountPath.StartsWith('/'))
        {
            problems.Add("config.mountPath must be an absolute path");
        }

        foreach (var file in manifest.Config.Files)
        {
            if (string.IsNullOrWhiteSpace(file) || file.Contains('/', StringComparison.Ordinal) || file.Contains('\\', StringComparison.Ordinal))
            {
                problems.Add($"config.files entry '{file}' must be a file name without directories");
            }
        }

        if (manifest.Probes.PeriodSeconds < 1 || manifest.Probes.TimeoutSeconds < 1 || manifest.Probes.StartupSeconds < manifest.Probes.PeriodSeconds)
        {
            problems.Add("probes: periodSeconds and timeoutSeconds must be at least 1 and startupSeconds at least periodSeconds");
        }

        if (manifest.Shutdown.GraceSeconds < 1)
        {
            problems.Add("shutdown.graceSeconds must be at least 1");
        }

        foreach (var (label, quantities) in new[] { ("requests", manifest.Resources?.Requests), ("limits", manifest.Resources?.Limits) })
        {
            foreach (var (field, value) in new[] { ("cpu", quantities?.Cpu), ("memory", quantities?.Memory) })
            {
                if (value is not null && !Quantity().IsMatch(value))
                {
                    problems.Add($"resources.{label}.{field} '{value}' is not a Kubernetes quantity");
                }
            }
        }

        if (manifest.Replicas < 1)
        {
            problems.Add("replicas must be at least 1");
        }

        if (manifest.Scaling is { } scaling && (scaling.Min < 1 || scaling.Max < scaling.Min || scaling.CpuPercent is < 1 or > 100))
        {
            problems.Add("scaling: min must be at least 1, max at least min and cpuPercent between 1 and 100");
        }

        if (manifest.Leader is { } leader && !DnsLabel().IsMatch(leader.Lease))
        {
            problems.Add($"leader.lease '{leader.Lease}' must be a DNS label");
        }

        foreach (var key in manifest.Environment.Keys)
        {
            if (!EnvironmentName().IsMatch(key))
            {
                problems.Add($"environment key '{key}' must match ^[A-Za-z_][A-Za-z0-9_]*$");
            }
        }

        return problems;
    }

    /// <summary>
    /// 不合法的清单。
    /// </summary>
    private static Error Invalid(string path, string reason)
        => Error.Validation(CliErrors.ManifestInvalid, CliMessages.ManifestInvalid, MessageArguments.Of(("path", path), ("reason", reason)));

    /// <summary>
    /// DNS 标签。
    /// </summary>
    [GeneratedRegex("^[a-z]([a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex DnsLabel();

    /// <summary>
    /// 绑定名。
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex BindingName();

    /// <summary>
    /// k8s 数量。
    /// </summary>
    [GeneratedRegex("^[0-9]+(\\.[0-9]+)?(m|k|Ki|M|Mi|G|Gi|T|Ti)?$")]
    private static partial Regex Quantity();

    /// <summary>
    /// 环境变量名。
    /// </summary>
    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentName();
}
