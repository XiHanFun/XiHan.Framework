// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Data.SqlSugar.Initializers;

/// <summary>
/// 表结构升级上下文：本次初始化里各连接的建表结果
/// </summary>
/// <remarks>
/// 新库是指本次初始化从零建出了全部实体表的连接：此前库里一张要建的表都没有，按当前实体建成即是最新结构。
/// 升级器据此把新库直接登记为最新版本，历史升级脚本只在它所属版本之前建的库上执行——
/// 否则每个历史脚本都得在最新结构上空转，任何一次改名都会让全新安装起不来。
/// </remarks>
public sealed class DbSchemaUpgradeContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="freshConfigIds">本次初始化从零建出全部实体表的连接配置标识</param>
    public DbSchemaUpgradeContext(IEnumerable<string> freshConfigIds)
    {
        ArgumentNullException.ThrowIfNull(freshConfigIds);
        FreshConfigIds = new HashSet<string>(freshConfigIds, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 本次初始化从零建出全部实体表的连接配置标识
    /// </summary>
    public IReadOnlySet<string> FreshConfigIds { get; }

    /// <summary>
    /// 指定连接是否是本次初始化新建的库
    /// </summary>
    /// <param name="configId">连接配置标识</param>
    /// <returns>新建的库返回 true</returns>
    public bool IsFresh(string configId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configId);
        return FreshConfigIds.Contains(configId.Trim());
    }
}
