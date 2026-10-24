// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-07 生成的源码：应用自己的 <c>ICurrentUser</c> / <c>ICurrentTenant</c> 与读 <c>IInvocationAccessor.Current</c> 的实现，成员与 4.x 同名；租户标识为字符串，<c>UserId</c> 仍是 <c>long?</c>（另给 <c>Subject</c>），<c>CurrentTenant.Change</c> 可用。
/// </summary>
internal static class V4CurrentContextGenerator
{
    /// <summary>
    /// 生成的四个文件名（在项目的 <c>Security</c> 目录下）。
    /// </summary>
    public static readonly string[] FileNames = ["ICurrentUser.cs", "CurrentUser.cs", "ICurrentTenant.cs", "CurrentTenant.cs"];

    /// <summary>
    /// 生成文件名 → 内容。
    /// </summary>
    public static IReadOnlyDictionary<string, string> Generate(string ns, string newLine)
        => new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ICurrentUser.cs"] = CurrentUserInterface(ns, newLine),
            ["CurrentUser.cs"] = CurrentUser(ns, newLine),
            ["ICurrentTenant.cs"] = CurrentTenantInterface(ns, newLine),
            ["CurrentTenant.cs"] = CurrentTenant(ns, newLine),
        };

    /// <summary>
    /// 文件头与 using。
    /// </summary>
    private static StringBuilder Header(string ns, string newLine, params string[] usings)
    {
        var builder = new StringBuilder();
        builder.Append("// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine).Append(newLine);
        foreach (var directive in usings)
        {
            builder.Append("using ").Append(directive).Append(';').Append(newLine);
        }

        if (usings.Length > 0)
        {
            builder.Append(newLine);
        }

        builder.Append("namespace ").Append(ns).Append(';').Append(newLine).Append(newLine);
        return builder;
    }

    /// <summary>
    /// 三行 summary，内容是「由工具生成」。
    /// </summary>
    private static void Summary(StringBuilder builder, string indent, string newLine)
    {
        builder.Append(indent).Append("/// <summary>").Append(newLine);
        builder.Append(indent).Append("/// ").Append(CliMessages.MigrateGenerated.L()).Append(newLine);
        builder.Append(indent).Append("/// </summary>").Append(newLine);
    }

    /// <summary>
    /// 一个带 summary 的接口成员。
    /// </summary>
    private static void Member(StringBuilder builder, string declaration, string newLine, bool first)
    {
        if (!first)
        {
            builder.Append(newLine);
        }

        Summary(builder, "    ", newLine);
        builder.Append("    ").Append(declaration).Append(newLine);
    }

    /// <summary>
    /// 一个 <c>inheritdoc</c> 的实现成员。
    /// </summary>
    private static void Implementation(StringBuilder builder, string declaration, string newLine)
    {
        builder.Append(newLine);
        builder.Append("    /// <inheritdoc />").Append(newLine);
        builder.Append("    ").Append(declaration).Append(newLine);
    }

    /// <summary>
    /// <c>ICurrentUser</c>：与 4.x 同名成员，<c>UserId</c> 仍是 <c>long?</c>（5.0 主体 ID 解析成数字），<c>Subject</c> 是主体 ID 原文，<c>TenantId</c> 为字符串。
    /// </summary>
    private static string CurrentUserInterface(string ns, string newLine)
    {
        var builder = Header(ns, newLine, "System.Security.Claims");
        Summary(builder, string.Empty, newLine);
        builder.Append("public interface ICurrentUser").Append(newLine).Append('{').Append(newLine);
        var members = new[]
        {
            "bool IsAuthenticated { get; }",
            "long? UserId { get; }",
            "string? Subject { get; }",
            "string? UserName { get; }",
            "string? Name { get; }",
            "string? SurName { get; }",
            "string? PhoneNumber { get; }",
            "bool PhoneNumberVerified { get; }",
            "string? Email { get; }",
            "bool EmailVerified { get; }",
            "string? TenantId { get; }",
            "string[] Roles { get; }",
            "Claim? FindClaim(string claimType);",
            "Claim[] FindClaims(string claimType);",
            "Claim[] GetAllClaims();",
            "bool IsInRole(string roleName);",
        };
        for (var index = 0; index < members.Length; index++)
        {
            Member(builder, members[index], newLine, index == 0);
        }

        builder.Append('}').Append(newLine);
        return builder.ToString();
    }

    /// <summary>
    /// <c>CurrentUser</c>：读当前调用的 <c>SecurityContext</c>，主体 ID 作 <c>Subject</c> 并解析成 <c>long? UserId</c>，其余按标准 ClaimTypes 与 4.x 默认声明名取。
    /// </summary>
    private static string CurrentUser(string ns, string newLine)
    {
        var builder = Header(ns, newLine, "System.Globalization", "System.Security.Claims", "XiHan.Framework.Kernel.Pipeline", "XiHan.Framework.Kernel.Security");
        Summary(builder, string.Empty, newLine);
        builder.Append("public sealed class CurrentUser(IInvocationAccessor accessor) : ICurrentUser").Append(newLine).Append('{').Append(newLine);
        builder.Append("    private const string EmailVerifiedClaim = \"email_verified\";").Append(newLine);
        builder.Append("    private const string PhoneNumberClaim = \"phone_number\";").Append(newLine);
        builder.Append("    private const string PhoneNumberVerifiedClaim = \"phone_number_verified\";").Append(newLine);
        builder.Append("    private const string TenantIdClaim = \"tenantid\";").Append(newLine).Append(newLine);
        builder.Append("    private SecurityContext Security => accessor.Current.TryGetValue(out var context) ? context.GetSecurity() : SecurityContext.Anonymous;").Append(newLine).Append(newLine);
        builder.Append("    private Principal Principal => Security.Principal;").Append(newLine);
        Implementation(builder, "public bool IsAuthenticated => Principal.IsAuthenticated;", newLine);
        Implementation(builder, "public long? UserId => long.TryParse(Subject, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) ? userId : null;", newLine);
        Implementation(builder, "public string? Subject => Principal.IsAuthenticated ? Principal.Id : null;", newLine);
        Implementation(builder, "public string? UserName => Find(ClaimTypes.Name);", newLine);
        Implementation(builder, "public string? Name => Find(ClaimTypes.GivenName);", newLine);
        Implementation(builder, "public string? SurName => Find(ClaimTypes.Surname);", newLine);
        Implementation(builder, "public string? PhoneNumber => Find(PhoneNumberClaim);", newLine);
        Implementation(builder, "public bool PhoneNumberVerified => string.Equals(Find(PhoneNumberVerifiedClaim), \"true\", StringComparison.OrdinalIgnoreCase);", newLine);
        Implementation(builder, "public string? Email => Find(ClaimTypes.Email);", newLine);
        Implementation(builder, "public bool EmailVerified => string.Equals(Find(EmailVerifiedClaim), \"true\", StringComparison.OrdinalIgnoreCase);", newLine);
        Implementation(builder, "public string? TenantId => Security.Tenant?.Id ?? Find(TenantIdClaim);", newLine);
        Implementation(builder, "public string[] Roles => [.. Principal.ClaimValues(ClaimTypes.Role).Distinct(StringComparer.Ordinal)];", newLine);
        Implementation(builder, "public Claim? FindClaim(string claimType) => GetAllClaims().FirstOrDefault(claim => claim.Type == claimType);", newLine);
        Implementation(builder, "public Claim[] FindClaims(string claimType) => [.. GetAllClaims().Where(claim => claim.Type == claimType)];", newLine);
        Implementation(builder, "public Claim[] GetAllClaims() => [.. Principal.Claims.Select(claim => new Claim(claim.Key, claim.Value))];", newLine);
        Implementation(builder, "public bool IsInRole(string roleName) => Principal.HasClaim(ClaimTypes.Role, roleName);", newLine);
        builder.Append(newLine);
        builder.Append("    private string? Find(string claimType) => Principal.ClaimValues(claimType).FirstOrDefault();").Append(newLine);
        builder.Append('}').Append(newLine);
        return builder.ToString();
    }

    /// <summary>
    /// <c>ICurrentTenant</c>：与 4.x 同名成员，<c>Id</c> 为字符串。
    /// </summary>
    private static string CurrentTenantInterface(string ns, string newLine)
    {
        var builder = Header(ns, newLine);
        Summary(builder, string.Empty, newLine);
        builder.Append("public interface ICurrentTenant").Append(newLine).Append('{').Append(newLine);
        Member(builder, "bool IsAvailable { get; }", newLine, first: true);
        Member(builder, "string? Id { get; }", newLine, first: false);
        Member(builder, "string? Name { get; }", newLine, first: false);
        Member(builder, "IDisposable Change(string? id, string? name = null);", newLine, first: false);
        builder.Append('}').Append(newLine);
        return builder.ToString();
    }

    /// <summary>
    /// <c>CurrentTenant</c>：默认读当前调用的租户；<c>Change</c> 用 AsyncLocal 覆盖栈在当前执行流里显式切换（压栈、释放弹栈），覆盖调用携带的租户。
    /// </summary>
    private static string CurrentTenant(string ns, string newLine)
    {
        var builder = Header(ns, newLine, "XiHan.Framework.Kernel.Pipeline", "XiHan.Framework.Kernel.Security");
        Summary(builder, string.Empty, newLine);
        builder.Append("public sealed class CurrentTenant(IInvocationAccessor accessor) : ICurrentTenant").Append(newLine).Append('{').Append(newLine);
        builder.Append("    private static readonly AsyncLocal<Override?> _override = new();").Append(newLine).Append(newLine);
        builder.Append("    private TenantContext? Invoked => accessor.Current.TryGetValue(out var context) ? context.GetSecurity().Tenant : null;").Append(newLine);
        Implementation(builder, "public bool IsAvailable => Id is not null;", newLine);
        Implementation(builder, "public string? Id => _override.Value is { } current ? current.Id : Invoked?.Id;", newLine);
        Implementation(builder, "public string? Name => _override.Value is { } current ? current.Name : Invoked?.DisplayName;", newLine);
        builder.Append(newLine);
        builder.Append("    /// <inheritdoc />").Append(newLine);
        builder.Append("    public IDisposable Change(string? id, string? name = null)").Append(newLine);
        builder.Append("    {").Append(newLine);
        builder.Append("        var parent = _override.Value;").Append(newLine);
        builder.Append("        _override.Value = new Override(string.IsNullOrWhiteSpace(id) ? null : id.Trim(), name, parent);").Append(newLine);
        builder.Append("        return new Restore(parent);").Append(newLine);
        builder.Append("    }").Append(newLine).Append(newLine);
        Summary(builder, "    ", newLine);
        builder.Append("    private sealed record Override(string? Id, string? Name, Override? Parent);").Append(newLine).Append(newLine);
        Summary(builder, "    ", newLine);
        builder.Append("    private sealed class Restore(Override? parent) : IDisposable").Append(newLine);
        builder.Append("    {").Append(newLine);
        builder.Append("        private bool _disposed;").Append(newLine).Append(newLine);
        builder.Append("        /// <inheritdoc />").Append(newLine);
        builder.Append("        public void Dispose()").Append(newLine);
        builder.Append("        {").Append(newLine);
        builder.Append("            if (_disposed)").Append(newLine);
        builder.Append("            {").Append(newLine);
        builder.Append("                return;").Append(newLine);
        builder.Append("            }").Append(newLine).Append(newLine);
        builder.Append("            _disposed = true;").Append(newLine);
        builder.Append("            _override.Value = parent;").Append(newLine);
        builder.Append("        }").Append(newLine);
        builder.Append("    }").Append(newLine);
        builder.Append('}').Append(newLine);
        return builder.ToString();
    }
}
