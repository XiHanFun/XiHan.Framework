// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Runtime.CompilerServices;

namespace XiHan.Framework.Persistence.InMemory;

/// <summary>
/// 实体的浅复制。
/// </summary>
/// <remarks>
/// 表与覆盖层只存副本、读取只交出副本：调用方在读出的实例上的修改（含仓储写入的修改时间、并发戳与删除标记）不经仓储写回就碰不到存储，回滚也就不会漏掉它们。副本按字段逐一复制（<see cref="object.MemberwiseClone"/>），标量与协议字段互不影响；集合、嵌套对象等引用类型字段（含聚合的领域事件缓冲）与原实例共享同一个对象，不在隔离范围内。经 <see cref="UnsafeAccessorAttribute"/> 调用，不用反射，裁剪与 AOT 安全。
/// </remarks>
internal static class EntityCopy
{
    /// <summary>
    /// 复制一个实体。
    /// </summary>
    public static TEntity Of<TEntity>(TEntity entity)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(entity);
        return (TEntity)MemberwiseClone(entity);
    }

    /// <summary>
    /// <see cref="object.MemberwiseClone"/> 的访问器。
    /// </summary>
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "MemberwiseClone")]
    private static extern object MemberwiseClone(object instance);
}
