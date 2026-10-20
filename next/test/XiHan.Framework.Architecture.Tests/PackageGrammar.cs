// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Architecture.Tests;

/// <summary>
/// 包 ID 文法与环层判定，对应总体方案 §5.3 与 spec/conventions/package-ids.md。
/// </summary>
internal static partial class PackageGrammar
{
    /// <summary>
    /// 角色段禁用词：这些词描述策略或泛指，不描述机制或技术。
    /// </summary>
    public static readonly IReadOnlySet<string> ForbiddenRoleWords = new HashSet<string>(StringComparer.Ordinal)
    {
        "Default", "Core", "Common", "Shared", "Base", "Impl", "Util", "Utils", "Helper", "Manager", "Bus", "Engine",
    };

    private static readonly Dictionary<string, string[]> FixedSuffixes = new(StringComparer.Ordinal)
    {
        ["Kernel"] = ["Analyzers", "Generators"],
        ["Hosting"] = ["AspNetCore", "CommandLine"],
        ["Protocols"] = ["Http", "Grpc", "Mcp", "CloudEvents"],
        ["Plugins"] = ["InProcess", "OutOfProcess", "Wasm"],
        ["Profiles"] = ["Minimal", "Standard", "Full"],
    };

    private static readonly HashSet<string> Standalone = new(StringComparer.Ordinal)
    {
        "Primitives", "Kernel", "Domain", "Testing", "Cli",
    };

    public static bool TryValidate(string packageId, IReadOnlySet<string> subsystems, out string reason)
    {
        if (!packageId.StartsWith(Workspace.PackagePrefix, StringComparison.Ordinal))
        {
            reason = $"必须以 {Workspace.PackagePrefix} 开头";
            return false;
        }

        var segments = packageId[Workspace.PackagePrefix.Length..].Split('.');
        var head = segments[0];

        if (segments.Length == 1)
        {
            if (Standalone.Contains(head))
            {
                reason = string.Empty;
                return true;
            }

            reason = subsystems.Contains(head)
                ? "子系统包必须带角色段（Abstractions、Conformance、Generators、机制或技术名）"
                : "不是术语表中的领域词";
            return false;
        }

        if (segments.Length != 2)
        {
            reason = "包 ID 最多两段：领域 + 角色";
            return false;
        }

        var role = segments[1];

        if (head == "Compat")
        {
            reason = "Compat 的角色段必须是 V + 大版本号";
            return CompatRole().IsMatch(role);
        }

        if (FixedSuffixes.TryGetValue(head, out var allowed))
        {
            reason = $"{head} 允许的角色段：{string.Join("、", allowed)}";
            return Array.IndexOf(allowed, role) >= 0;
        }

        if (!subsystems.Contains(head))
        {
            reason = "不是术语表中的子系统领域词";
            return false;
        }

        if (!RoleSegment().IsMatch(role))
        {
            reason = "角色段必须是 PascalCase 标识符";
            return false;
        }

        if (ForbiddenRoleWords.Contains(role))
        {
            reason = $"角色段禁用词：{role}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// 环层：0 原语，1 内核，2 子系统抽象与领域，3 实现，4 宿主与协议，5 插件，6 Profile，7 工具链（可引用任何环）。
    /// </summary>
    public static int Ring(string packageId)
    {
        var segments = packageId[Workspace.PackagePrefix.Length..].Split('.');
        var head = segments[0];
        var role = segments.Length > 1 ? segments[1] : string.Empty;

        return head switch
        {
            "Primitives" => 0,
            "Kernel" when role.Length == 0 => 1,
            "Kernel" => 7,
            "Domain" => 2,
            "Hosting" or "Protocols" => 4,
            "Plugins" => 5,
            "Profiles" => 6,
            "Testing" or "Cli" or "Compat" => 7,
            _ => role is "Abstractions" ? 2 : role is "Conformance" or "Generators" ? 7 : 3,
        };
    }

    public static bool IsAbstractions(string packageId) => packageId.EndsWith(".Abstractions", StringComparison.Ordinal);

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex RoleSegment();

    [GeneratedRegex("^V[1-9][0-9]*$")]
    private static partial Regex CompatRole();
}
