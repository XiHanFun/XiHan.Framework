// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Primitives.Manifests;

/// <summary>
/// <c>xihan.manifest.json</c> 的对象模型，与 <c>spec/manifest/manifest.schema.json</c> 一一对应。
/// 只做数据与格式校验；收集、解析与求解是内核的职责。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public sealed partial record Manifest
{
    /// <summary>
    /// 清单 ID：<c>xihan.&lt;领域&gt;.&lt;名称&gt;</c>。
    /// </summary>
    public required string Id
    {
        get => field;
        init => field = ManifestId.Validate(value);
    }

    /// <summary>
    /// 所属包 ID。
    /// </summary>
    public string? Package { get; init; }

    /// <summary>
    /// 包版本（SemVer 2.0）。
    /// </summary>
    public required string Version
    {
        get => field;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = SemVerPattern().IsMatch(value)
                ? value
                : throw new ArgumentException($"版本 '{value}' 不是合法的 SemVer。", nameof(value));
        }
    }

    /// <summary>
    /// 依赖的原语层大版本。
    /// </summary>
    public required int Primitives
    {
        get => field;
        init => field = value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "原语层大版本必须是正整数。");
    }

    /// <summary>
    /// 稳定性。
    /// </summary>
    public required Stability Stability { get; init; }

    /// <summary>
    /// 种类。
    /// </summary>
    public required ManifestKind Kind { get; init; }

    /// <summary>
    /// 在能力解析中的角色。
    /// </summary>
    public ManifestRole Role { get; init; } = ManifestRole.Provider;

    /// <summary>
    /// 插件隔离边界；仅 <see cref="ManifestKind.Plugin"/> 需要。
    /// </summary>
    public PluginIsolation? Isolation { get; init; }

    /// <summary>
    /// 提供的能力。
    /// </summary>
    public IReadOnlyList<CapabilityDeclaration> Provides { get; init; } = [];

    /// <summary>
    /// 依赖的能力。
    /// </summary>
    public IReadOnlyList<CapabilityRequirement> Requires { get; init; } = [];

    /// <summary>
    /// 需要宿主注入的资源。
    /// </summary>
    public IReadOnlyList<Binding> Bindings { get; init; } = [];

    /// <summary>
    /// 贡献点。
    /// </summary>
    public Contributions Contributes { get; init; } = Contributions.Empty;

    /// <summary>
    /// 激活事件；默认 <c>onStartup</c>。
    /// </summary>
    public IReadOnlyList<string> Activation
    {
        get => field;
        init => field = ValidateAll(value, ActivationPattern(), "激活事件");
    } = ["onStartup"];

    /// <summary>
    /// 插件所需权限；由宿主配置授予。
    /// </summary>
    public IReadOnlyList<string> Permissions
    {
        get => field;
        init => field = ValidateAll(value, PermissionPattern(), "权限");
    } = [];

    /// <summary>
    /// 入口类型的完全限定名。
    /// </summary>
    public string? Entry { get; init; }

    /// <summary>
    /// 检查跨字段约束：插件必须声明隔离边界；非插件不得声明隔离边界或权限；同一能力不得重复提供或重复依赖。
    /// </summary>
    public Result Validate()
    {
        if (Kind == ManifestKind.Plugin && Isolation is null)
        {
            return Error.Validation(ErrorCode.InvalidArgument, $"插件清单 '{Id}' 必须声明 isolation。");
        }

        if (Kind != ManifestKind.Plugin && (Isolation is not null || Permissions.Count > 0))
        {
            return Error.Validation(ErrorCode.InvalidArgument, $"清单 '{Id}' 不是插件，不得声明 isolation 或 permissions。");
        }

        if (HasDuplicates(Provides.Select(static p => p.Id)))
        {
            return Error.Validation(ErrorCode.InvalidArgument, $"清单 '{Id}' 的 provides 有重复的能力 ID。");
        }

        if (HasDuplicates(Requires.Select(static r => r.Id)))
        {
            return Error.Validation(ErrorCode.InvalidArgument, $"清单 '{Id}' 的 requires 有重复的能力 ID。");
        }

        if (HasDuplicates(Bindings.Select(static b => b.Name)))
        {
            return Error.Validation(ErrorCode.InvalidArgument, $"清单 '{Id}' 的 bindings 有重复的绑定名。");
        }

        return Result.Success();
    }

    private static IReadOnlyList<string> ValidateAll(IReadOnlyList<string> values, Regex pattern, string what)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            if (value is null || !pattern.IsMatch(value))
            {
                throw new ArgumentException($"{what} '{value}' 不符合格式。", nameof(values));
            }
        }

        return values;
    }

    private static bool HasDuplicates(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!seen.Add(value))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+(-[0-9A-Za-z.-]+)?(\\+[0-9A-Za-z.-]+)?$")]
    private static partial Regex SemVerPattern();

    [GeneratedRegex("^(onStartup|onOperation:[a-z0-9.*-]+|onEvent:[a-z0-9.*-]+)$")]
    private static partial Regex ActivationPattern();

    [GeneratedRegex("^(net\\.outbound|fs\\.read|fs\\.write|capability):.+$")]
    private static partial Regex PermissionPattern();
}
