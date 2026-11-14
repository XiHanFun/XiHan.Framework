// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Frozen;
using System.Text.Json.Serialization;

namespace XiHan.Framework.Security.Database.Grants;

/// <summary>
/// 一个主体在一个租户下生效的授予，作为缓存值
/// </summary>
/// <remarks>
/// 跨进程缓存序列化 <see cref="Permissions"/>；调用方拿到的是由它建的冻结集合，同一个缓存对象只建一次（两级缓存的一级缓存里反复命中时不再分配）
/// </remarks>
internal sealed class GrantSetEntry
{
    /// <summary>
    /// 创建
    /// </summary>
    public GrantSetEntry(string[] permissions)
    {
        Permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
    }

    /// <summary>
    /// 权限码，按序数排序
    /// </summary>
    public string[] Permissions { get; }

    /// <summary>
    /// 不可变的授予集合
    /// </summary>
    /// <remarks>
    /// 第一次访问时建；并发的第一次访问可能各建一份，内容相同，后写的覆盖先写的
    /// </remarks>
    [JsonIgnore]
    public FrozenSet<string> Set => field ??= Permissions.ToFrozenSet(StringComparer.Ordinal);
}
