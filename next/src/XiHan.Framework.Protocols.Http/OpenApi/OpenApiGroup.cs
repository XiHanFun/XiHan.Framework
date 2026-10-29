// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text.RegularExpressions;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Protocols.Http.OpenApi;

/// <summary>
/// OpenAPI 文档的一个分组。
/// </summary>
/// <remarks>
/// 操作按归属特性（<c>OperationDescriptor.OwnerId</c>）入组，普通端点按标签入组：组的标签是组内操作的资源段加 <see cref="Tags"/>。
/// 每组另发一份文档，全量文档的 <c>x-tagGroups</c> 按 <see cref="Order"/>、再按 <see cref="Name"/> 列出各组。
/// 显示名与说明在消息目录 <c>openapi.groups.&lt;组名&gt;.name</c> / <c>.description</c>（组名的 <c>-</c> 换 <c>_</c>），没有显示名时用组名（ADR-0052）。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public sealed partial record OpenApiGroup
{
    /// <summary>
    /// 创建分组。
    /// </summary>
    /// <param name="name">组名：小写字母开头，小写字母、数字与连字符；同时是文档地址的最后一段。</param>
    /// <param name="order">排序，越小越前。</param>
    /// <param name="owners">所属特性的清单 ID。</param>
    /// <param name="tags">额外的标签（让没有操作的普通端点入组）；为空表示没有。</param>
    public OpenApiGroup(string name, int order, IReadOnlyList<string> owners, IReadOnlyList<string>? tags = null)
    {
        Name = name is not null && NamePattern().IsMatch(name) ? name : throw new ArgumentException($"The OpenAPI group name '{name}' must start with a lowercase letter and contain only lowercase letters, digits and hyphens.", nameof(name));
        ArgumentNullException.ThrowIfNull(owners);
        Order = order;
        Owners = owners.All(static o => !string.IsNullOrWhiteSpace(o)) ? [.. owners] : throw new ArgumentException("Owner IDs must not be blank.", nameof(owners));
        Tags = tags is null ? [] : tags.All(static t => !string.IsNullOrWhiteSpace(t)) ? [.. tags] : throw new ArgumentException("Tags must not be blank.", nameof(tags));
    }

    /// <summary>
    /// 组名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 排序，越小越前。
    /// </summary>
    public int Order { get; }

    /// <summary>
    /// 所属特性的清单 ID。
    /// </summary>
    public IReadOnlyList<string> Owners { get; }

    /// <summary>
    /// 额外的标签。
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// 组名的匹配。
    /// </summary>
    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex NamePattern();
}
