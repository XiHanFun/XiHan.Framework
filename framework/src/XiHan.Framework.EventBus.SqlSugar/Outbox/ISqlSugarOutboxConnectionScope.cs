// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.EventBus.SqlSugar.Outbox;

/// <summary>
/// 发件箱入箱连接范围
/// </summary>
/// <remarks>
/// 在 <see cref="Use"/> 返回的对象释放前，于同一异步流程中入箱的事件写入指定连接。
/// 指定的连接必须已登记在当前工作单元，否则入箱被拒绝。
/// </remarks>
public interface ISqlSugarOutboxConnectionScope
{
    /// <summary>
    /// 当前范围指定的连接配置标识，未指定时为 null
    /// </summary>
    string? ConfigId { get; }

    /// <summary>
    /// 指定入箱写入的连接
    /// </summary>
    /// <param name="configId">承载业务数据的连接配置标识，须与连接配置标识原文一致（区分大小写）</param>
    /// <returns>释放时恢复外层指定连接的对象</returns>
    /// <exception cref="ArgumentException"><paramref name="configId"/> 为 null、空字符串或空白</exception>
    IDisposable Use(string configId);
}
