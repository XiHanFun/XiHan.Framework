// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Data;

namespace XiHan.Framework.Uow.Options;

/// <summary>
/// 工作单元选项接口
/// </summary>
public interface IXiHanUnitOfWorkOptions
{
    /// <summary>
    /// 是否启用事务
    /// </summary>
    bool IsTransactional { get; }

    /// <summary>
    /// 事务隔离级别
    /// </summary>
    IsolationLevel? IsolationLevel { get; }

    /// <summary>
    /// 超时时间
    /// </summary>
    int? Timeout { get; }

    /// <summary>
    /// 是否必须使用与外层工作单元互相独立的物理连接
    /// </summary>
    /// <remarks>
    /// 由 <c>IUnitOfWorkManager.Begin(options, requiresNew: true)</c> 在已存在环境工作单元时置位，调用方不直接设置。
    /// <para>
    /// 数据访问层据此为本工作单元物化一条**新的**数据库连接，而不是复用外层已经开启事务的那条：
    /// 同一连接上无法嵌套事务，复用只会让内层提交退化为空操作，写入最终仍由外层事务决定去留。
    /// </para>
    /// <para>
    /// <b>调用方义务</b>：独立连接意味着内层与外层是数据库眼中<b>两个互不知情的事务</b>，因此
    /// </para>
    /// <list type="number">
    ///   <item>
    ///     内层提交后不再受外层回滚影响——这正是 <c>requiresNew</c> 的目的，但也意味着
    ///     外层失败时内层写入会留存，调用方必须能接受这种「部分成功」。
    ///   </item>
    ///   <item>
    ///     内外两条事务若触及<b>同一批行</b>会互相等待，且这个环有一半在应用线程上，
    ///     数据库的死锁检测看不到，表现为挂起直到锁等待超时。
    ///     不要在已经修改过某些行的事务里，再用 <c>requiresNew</c> 去改同一批行。
    ///   </item>
    ///   <item>
    ///     库级单写者的数据库（如 SQLite）上，同库的内外两个事务必然冲突，不适用本模式。
    ///   </item>
    /// </list>
    /// </remarks>
    bool RequiresIsolatedConnection { get; }
}
