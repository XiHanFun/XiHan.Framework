// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 内存表的非泛型视图。
/// </summary>
/// <remarks>
/// 健康项按它遍历各实体的表比较用量与容量，工作单元提交时按它给涉及的表依次加锁，都不必知道实体与键的类型。
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

    /// <summary>
    /// 一次提交涉及多张表时的加锁次序。
    /// </summary>
    /// <remarks>
    /// 小的先锁。
    /// </remarks>
    long Order { get; }

    /// <summary>
    /// 保护表内行的锁。
    /// </summary>
    /// <remarks>
    /// 提交时持有它完成校验与应用。
    /// </remarks>
    Lock Gate { get; }
}
