// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions;

/// <summary>
/// 授予的主体：用户或角色。
/// </summary>
/// <param name="Kind"><see cref="User"/> 或 <see cref="Role"/>。</param>
/// <param name="Id">用户 ID 或角色名；区分大小写。</param>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct GrantSubject(string Kind, string Id)
{
    /// <summary>用户。</summary>
    public const string User = "user";

    /// <summary>角色。</summary>
    public const string Role = "role";

    /// <summary>
    /// 种类。
    /// </summary>
    public string Kind { get; } = Kind is User or Role ? Kind : throw new ArgumentException($"The subject kind must be '{User}' or '{Role}', but was '{Kind}'.", nameof(Kind));

    /// <summary>
    /// ID。
    /// </summary>
    public string Id { get; } = !string.IsNullOrWhiteSpace(Id) ? Id : throw new ArgumentException("The subject ID must not be empty.", nameof(Id));

    /// <summary>
    /// 用户主体。
    /// </summary>
    public static GrantSubject ForUser(string userId) => new(User, userId);

    /// <summary>
    /// 角色主体。
    /// </summary>
    public static GrantSubject ForRole(string role) => new(Role, role);

    /// <inheritdoc />
    public override string ToString() => $"{Kind}:{Id}";
}
