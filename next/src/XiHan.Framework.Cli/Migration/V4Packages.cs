// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 4.x 包 → 5.0 包的映射（<c>spec/migration/v4.md</c> §4 V4-10），以及 5.0 的正式包清单（判断 <c>using</c> 是否还存在）。
/// </summary>
internal static class V4Packages
{
    /// <summary>
    /// 包 ID 前缀。
    /// </summary>
    public const string Prefix = "XiHan.Framework.";

    /// <summary>
    /// 4.x 包名（去掉前缀）→ 5.0 包名（去掉前缀）列表；空列表表示删除。按最长前缀匹配，子包（<c>Bot.DingTalk</c>）沿用父包的映射。
    /// </summary>
    private static readonly (string Legacy, string[] Targets)[] _map =
    [
        ("Core", ["Kernel"]),
        ("Utils", ["Kernel"]),
        ("Castle", ["Kernel"]),
        ("Metadata", ["Primitives"]),
        ("Domain.Shared", ["Domain"]),
        ("Domain", ["Domain"]),
        ("Application.Contracts", ["Kernel"]),
        ("Application", ["Kernel"]),
        ("Data", ["Persistence.Abstractions"]),
        ("Uow", ["Persistence.Abstractions"]),
        ("DistributedIds", ["Persistence.Abstractions"]),
        ("Caching", ["Caching.Abstractions"]),
        ("EventBus", ["Messaging.Abstractions"]),
        ("Messaging", ["Messaging.Abstractions"]),
        ("Authentication", ["Security.Abstractions"]),
        ("Authorization", ["Security.Abstractions"]),
        ("Security", ["Security.Abstractions"]),
        ("MultiTenancy", ["MultiTenancy.Abstractions"]),
        ("Localization", ["Localization.Abstractions"]),
        ("ObjectStorage", ["ObjectStorage.Abstractions"]),
        ("Tasks", ["Scheduling.Abstractions"]),
        ("Auditing", ["Auditing.Abstractions"]),
        ("Settings", ["Configuration.Abstractions"]),
        ("Observability", ["Observability.OpenTelemetry"]),
        ("Logging", ["Observability.OpenTelemetry"]),
        ("Web.Core", ["Hosting.AspNetCore", "Protocols.Http"]),
        ("Web.Api", ["Hosting.AspNetCore", "Protocols.Http"]),
        ("Web.Docs", ["Protocols.Http"]),
        ("Web.Grpc", ["Protocols.Grpc"]),
        ("Web.Mcp", ["Protocols.Mcp"]),
        ("Web.RealTime", []),
        ("Web.Gateway", []),
        ("Http", []),
        ("Serialization", []),
        ("Threading", []),
        ("Timing", []),
        ("Validation", []),
        ("ObjectMapping", []),
        ("Script", []),
        ("Templating", []),
        ("SearchEngines", []),
        ("VirtualFileSystem", []),
        ("Upgrade", []),
        ("AI", []),
        ("Bot", []),
        ("Analyzers", []),
        ("DevTools", []),
        ("Traffic", []),
        ("Workflow", []),
    ];

    /// <summary>
    /// 5.0 正式包（去掉前缀）；命名空间等于包 ID 或其下一层。
    /// </summary>
    public static readonly IReadOnlySet<string> NextPackages = new HashSet<string>(StringComparer.Ordinal)
    {
        "Primitives", "Kernel", "Domain", "Testing", "Cli",
        "Persistence.Abstractions", "Persistence.InMemory", "Persistence.EntityFrameworkCore", "Persistence.SqlSugar", "Persistence.Conformance", "Persistence.Generators",
        "Caching.Abstractions", "Caching.InMemory", "Caching.Redis", "Caching.Remote", "Caching.Conformance",
        "MultiTenancy.Abstractions", "MultiTenancy.InMemory", "MultiTenancy.Conformance",
        "Security.Abstractions", "Security.InMemory", "Security.Jwt", "Security.OpenIdConnect", "Security.Conformance",
        "Messaging.Abstractions", "Messaging.InMemory", "Messaging.RabbitMQ", "Messaging.Conformance",
        "Localization.Abstractions", "Localization.InMemory", "Localization.Database", "Localization.Conformance",
        "ObjectStorage.Abstractions", "ObjectStorage.FileSystem", "ObjectStorage.Conformance",
        "Scheduling.Abstractions", "Scheduling.InMemory", "Scheduling.Kubernetes", "Scheduling.Conformance",
        "Configuration.Abstractions", "Configuration.Conformance",
        "Auditing.Abstractions", "Auditing.InMemory", "Auditing.Conformance",
        "Workflow.Abstractions", "Workflow.InMemory", "Workflow.Conformance",
        "Observability.OpenTelemetry",
        "Hosting.AspNetCore", "Hosting.CommandLine",
        "Protocols.Http", "Protocols.Grpc", "Protocols.Mcp", "Protocols.CloudEvents",
        "Plugins.Protocol", "Plugins.InProcess", "Plugins.OutOfProcess", "Plugins.Wasm", "Plugins.Conformance",
        "Profiles.Minimal", "Profiles.Standard", "Profiles.Full",
        "Kernel.Analyzers", "Kernel.Generators",
    };

    /// <summary>
    /// 一个 4.x 包 ID 的 5.0 去处；不是框架包（没有映射项）返回 <see langword="null"/>，删除返回空列表，与 5.0 同名的返回自身（只改版本）。
    /// </summary>
    public static IReadOnlyList<string>? Map(string packageId)
    {
        if (!packageId.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = packageId[Prefix.Length..];
        // 与 5.0 同名（Domain、各 .Abstractions）：保留名字，只把版本改成 5.0；已经迁过的项目再跑一遍不会被改坏
        if (NextPackages.Contains(rest))
        {
            return [packageId];
        }

        foreach (var (legacy, targets) in _map.OrderByDescending(static m => m.Legacy.Length))
        {
            if (rest == legacy || rest.StartsWith(legacy + ".", StringComparison.Ordinal))
            {
                return targets.Select(static t => Prefix + t).ToList();
            }
        }

        return null;
    }

    /// <summary>
    /// 5.0 里没有子命名空间的包：4.x 同名包的子命名空间（<c>Domain.Shared</c>、<c>Domain.Entities</c>）都是旧的。
    /// </summary>
    private static readonly string[] _flatPackages = ["Domain"];

    /// <summary>
    /// 命名空间是否属于 5.0 正式包（等于包 ID，或在其子命名空间里；没有子命名空间的包只认等于）。
    /// </summary>
    public static bool IsNextNamespace(string ns)
    {
        if (!ns.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return true;
        }

        // 逐级去掉末段：XiHan.Framework.Kernel.Security.Claims → Kernel.Security → Kernel
        var rest = ns[Prefix.Length..];
        if (_flatPackages.Any(package => rest.StartsWith(package + ".", StringComparison.Ordinal)))
        {
            return false;
        }
        while (rest.Length > 0)
        {
            if (NextPackages.Contains(rest))
            {
                return true;
            }

            var dot = rest.LastIndexOf('.');
            rest = dot > 0 ? rest[..dot] : string.Empty;
        }

        return false;
    }
}
