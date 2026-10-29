// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;

namespace XiHan.Framework.Architecture.Tests.Model;

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

    /// <summary>
    /// 结构词领域的固定角色段：这些领域不接受任意机制名，只允许枚举出的后缀。
    /// </summary>
    private static readonly Dictionary<string, string[]> _fixedSuffixes = new(StringComparer.Ordinal)
    {
        ["Kernel"] = ["Analyzers", "Generators"],
        ["Hosting"] = ["AspNetCore", "CommandLine"],
        ["Protocols"] = ["Http", "Grpc", "Mcp", "CloudEvents"],
        ["Plugins"] = ["InProcess", "OutOfProcess", "Wasm", "Protocol", "Conformance"],
        ["Profiles"] = ["Minimal", "Standard", "Full"],
    };

    /// <summary>
    /// 允许单段出现（不带角色段）的包名。
    /// </summary>
    private static readonly HashSet<string> _standalone = new(StringComparer.Ordinal)
    {
        "Primitives", "Kernel", "Domain", "Testing", "Cli",
    };

    /// <summary>
    /// 校验包 ID 是否符合文法。
    /// </summary>
    /// <remarks>
    /// 不符合时 <paramref name="reason"/> 说明原因。
    /// </remarks>
    /// <param name="packageId">包 ID。</param>
    /// <param name="subsystems">术语表中的子系统领域词。</param>
    /// <param name="reason">不符合时的原因；符合时为空字符串。</param>
    public static bool TryValidate(string packageId, IReadOnlySet<string> subsystems, out string reason)
    {
        if (!packageId.StartsWith(Workspace.PackagePrefix, StringComparison.Ordinal))
        {
            reason = $"The package ID must start with '{Workspace.PackagePrefix}'.";
            return false;
        }

        var segments = packageId[Workspace.PackagePrefix.Length..].Split('.');
        var head = segments[0];

        // 单段：只有少数结构词可以独立成包；子系统领域词必须带角色段。
        if (segments.Length == 1)
        {
            if (_standalone.Contains(head))
            {
                reason = string.Empty;
                return true;
            }

            reason = subsystems.Contains(head)
                ? "A subsystem package must have a role segment (Abstractions, Conformance, Generators, or a mechanism or technology name)."
                : "The head segment is not a domain word in the vocabulary.";
            return false;
        }

        if (segments.Length != 2)
        {
            reason = "A package ID has at most two segments: domain and role.";
            return false;
        }

        var role = segments[1];

        // 固定后缀的结构词先于一般子系统规则判定，它们的角色段是封闭集合。
        if (_fixedSuffixes.TryGetValue(head, out var allowed))
        {
            reason = $"The role segments allowed for '{head}' are: {string.Join(", ", allowed)}.";
            return Array.IndexOf(allowed, role) >= 0;
        }

        if (!subsystems.Contains(head))
        {
            reason = "The head segment is not a subsystem domain word in the vocabulary.";
            return false;
        }

        if (!RoleSegment().IsMatch(role))
        {
            reason = "The role segment must be a PascalCase identifier.";
            return false;
        }

        if (ForbiddenRoleWords.Contains(role))
        {
            reason = $"The role segment uses a forbidden word: '{role}'.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// 环层。
    /// </summary>
    /// <remarks>
    /// 0 原语，1 内核，2 子系统抽象与领域，3 实现，4 宿主与协议，5 插件，6 Profile，7 工具链（可引用任何环）。
    /// </remarks>
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
            "Hosting" => 4,
            "Plugins" when role == "Protocol" => 3,
            "Plugins" when role == "Conformance" => 7,
            "Protocols" or "Plugins" => 5,
            "Profiles" => 6,
            "Testing" or "Cli" => 7,
            _ => role is "Abstractions" ? 2 : role is "Conformance" or "Generators" ? 7 : 3,
        };
    }

    /// <summary>
    /// 契约包。
    /// </summary>
    /// <remarks>
    /// 同环之间只能经契约包往来。<c>.Abstractions</c> 之外，术语表把 <c>Domain</c> 定义为纯契约、无实现包，同样算契约包。
    /// </remarks>
    public static bool IsContract(string packageId)
        => packageId.EndsWith(".Abstractions", StringComparison.Ordinal) || packageId == Workspace.PackagePrefix + "Domain";

    /// <summary>
    /// 角色段的正则：PascalCase 标识符。
    /// </summary>
    [GeneratedRegex("^[A-Z][A-Za-z0-9]*$")]
    private static partial Regex RoleSegment();
}
