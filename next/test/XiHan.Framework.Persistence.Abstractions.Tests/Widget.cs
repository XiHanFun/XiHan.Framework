// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Domain.Entities;

namespace XiHan.Framework.Persistence.Abstractions.Tests;

/// <summary>
/// 不实现任何协议的测试实体，用于验证约定对普通实体不生效。
/// </summary>
internal sealed class Widget : IEntity<Guid>
{
    /// <inheritdoc />
    public Guid Id { get; init; }

    /// <summary>
    /// 名称。
    /// </summary>
    /// <remarks>
    /// 查询测试按它过滤与排序。
    /// </remarks>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 排名。
    /// </summary>
    /// <remarks>
    /// 查询测试按它过滤与排序。
    /// </remarks>
    public int Rank { get; set; }
}
