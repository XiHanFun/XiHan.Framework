// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// V4-07 生成的源码：应用自己的 <c>ICurrentUser</c> / <c>ICurrentTenant</c> 与读 <c>IInvocationAccessor.Current</c> 的实现，成员与 4.x 同名、标识改为字符串。
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
    /// <c>ICurrentUser</c>：与 4.x 同名成员，<c>UserId</c> / <c>TenantId</c> 为字符串。
    /// </summary>
    private static string CurrentUserInterface(string ns, string newLine)
    {
        var builder = Header(ns, newLine, "System.Security.Claims");
        Summary(builder, string.Empty, newLine);
        builder.Append("public interface ICurrentUser").Append(newLine).Append('{').Append(newLine);
        var members = new[]
        {
            "bool IsAuthenticated { get; }",
            "string? UserId { get; }",
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
    /// <c>CurrentUser</c>：读当前调用的 <c>SecurityContext</c>，主体 ID 作 <c>UserId</c>，其余按标准 ClaimTypes 与 4.x 默认声明名取。
    /// </summary>
    private static string CurrentUser(string ns, string newLine)
    {
        var builder = Header(ns, newLine, "System.Security.Claims", "XiHan.Framework.Kernel.Pipeline", "XiHan.Framework.Kernel.Security");
        Summary(builder, string.Empty, newLine);
        builder.Append("public sealed class CurrentUser(IInvocationAccessor accessor) : ICurrentUser").Append(newLine).Append('{').Append(newLine);
        builder.Append("    private const string EmailVerifiedClaim = \"email_verified\";").Append(newLine);
        builder.Append("    private const string PhoneNumberClaim = \"phone_number\";").Append(newLine);
        builder.Append("    private const string PhoneNumberVerifiedClaim = \"phone_number_verified\";").Append(newLine);
        builder.Append("    private const string TenantIdClaim = \"tenantid\";").Append(newLine).Append(newLine);
        builder.Append("    private SecurityContext Security => accessor.Current.TryGetValue(out var context) ? context.GetSecurity() : SecurityContext.Anonymous;").Append(newLine).Append(newLine);
        builder.Append("    private Principal Principal => Security.Principal;").Append(newLine);
        Implementation(builder, "public bool IsAuthenticated => Principal.IsAuthenticated;", newLine);
        Implementation(builder, "public string? UserId => Principal.IsAuthenticated ? Principal.Id : null;", newLine);
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
    /// <c>CurrentTenant</c>：读当前调用的租户；租户随调用的 Baggage 走，<c>Change</c> 不支持。
    /// </summary>
    private static string CurrentTenant(string ns, string newLine)
    {
        var builder = Header(ns, newLine, "XiHan.Framework.Kernel.Pipeline", "XiHan.Framework.Kernel.Security");
        Summary(builder, string.Empty, newLine);
        builder.Append("public sealed class CurrentTenant(IInvocationAccessor accessor) : ICurrentTenant").Append(newLine).Append('{').Append(newLine);
        builder.Append("    private TenantContext? Tenant => accessor.Current.TryGetValue(out var context) ? context.GetSecurity().Tenant : null;").Append(newLine);
        Implementation(builder, "public bool IsAvailable => Tenant is not null;", newLine);
        Implementation(builder, "public string? Id => Tenant?.Id;", newLine);
        Implementation(builder, "public string? Name => Tenant?.DisplayName;", newLine);
        Implementation(builder, "public IDisposable Change(string? id, string? name = null) => throw new NotSupportedException(\"The tenant follows the invocation's baggage and cannot be changed inside a call; start a new invocation with the tenant baggage instead.\");", newLine);
        builder.Append('}').Append(newLine);
        return builder.ToString();
    }
}
