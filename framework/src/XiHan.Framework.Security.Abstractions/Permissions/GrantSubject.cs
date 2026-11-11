// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Permissions;

/// <summary>
/// 授予的主体：用户或角色
/// </summary>
/// <remarks>
/// <see langword="default"/> 值不是合法主体，读取 <see cref="Kind"/> 或 <see cref="Id"/> 抛出 <see cref="InvalidOperationException"/>（RFC-0001「标识值」）
/// </remarks>
/// <param name="Kind"><see cref="User"/> 或 <see cref="Role"/></param>
/// <param name="Id">用户 ID 或角色名；区分大小写</param>
[ApiLevel(Stability.Stable, "5.0")]
public readonly record struct GrantSubject(string Kind, string Id)
{
    /// <summary>
    /// 用户
    /// </summary>
    public const string User = "user";

    /// <summary>
    /// 角色
    /// </summary>
    public const string Role = "role";

    /// <summary>
    /// 校验过的种类
    /// </summary>
    /// <remarks>
    /// 只有 <see langword="default"/> 值为 <see langword="null"/>
    /// </remarks>
    private readonly string? _kind = Kind is User or Role ? Kind : throw new ArgumentException($"The subject kind must be '{User}' or '{Role}', but was '{Kind}'.", nameof(Kind));

    /// <summary>
    /// 校验过的 ID
    /// </summary>
    /// <remarks>
    /// 只有 <see langword="default"/> 值为 <see langword="null"/>
    /// </remarks>
    private readonly string? _id = !string.IsNullOrWhiteSpace(Id) ? Id : throw new ArgumentException("The subject ID must not be empty.", nameof(Id));

    /// <summary>
    /// 种类
    /// </summary>
    public string Kind => _kind ?? throw new InvalidOperationException("The 'GrantSubject' is the default value and has no kind; create it with the constructor.");

    /// <summary>
    /// ID
    /// </summary>
    public string Id => _id ?? throw new InvalidOperationException("The 'GrantSubject' is the default value and has no ID; create it with the constructor.");

    /// <summary>
    /// 用户主体
    /// </summary>
    public static GrantSubject ForUser(string userId) => new(User, userId);

    /// <summary>
    /// 角色主体
    /// </summary>
    public static GrantSubject ForRole(string role) => new(Role, role);

    /// <inheritdoc />
    public override string ToString() => _kind is null ? string.Empty : $"{_kind}:{_id}";
}
