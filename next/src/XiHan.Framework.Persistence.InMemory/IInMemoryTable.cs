// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存表的非泛型视图。
/// </summary>
/// <remarks>
/// 健康项按它遍历各实体的表比较用量与容量，不必知道实体与键的类型。
/// </remarks>
internal interface IInMemoryTable
{
    /// <summary>
    /// 实体类型名。
    /// </summary>
    string EntityName { get; }

    /// <summary>
    /// 当前条数。
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 容量上限。
    /// </summary>
    int Capacity { get; }
}
