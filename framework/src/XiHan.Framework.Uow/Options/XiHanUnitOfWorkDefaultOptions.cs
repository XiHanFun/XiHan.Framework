#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanUnitOfWorkDefaultOptions
// Guid:f3d3bccf-5d84-4aea-90b4-771b4a448b3d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/01 20:44:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Data;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.Uow.Options;

/// <summary>
/// 工作单元默认选项
/// </summary>
public class XiHanUnitOfWorkDefaultOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "XiHan:Uow:Default";

    /// <summary>
    /// 事务行为
    /// </summary>
    public UnitOfWorkTransactionBehavior TransactionBehavior { get; set; } = UnitOfWorkTransactionBehavior.Auto;

    /// <summary>
    /// 隔离级别
    /// </summary>
    public IsolationLevel? IsolationLevel { get; set; }

    /// <summary>
    /// 超时时间 (单位：毫秒)
    /// </summary>
    public int? Timeout { get; set; }

    /// <summary>
    /// 计算是否事务性
    /// </summary>
    /// <param name="autoValue"></param>
    /// <returns></returns>
    /// <exception cref="XiHanException"></exception>
    public bool CalculateIsTransactional(bool autoValue)
    {
        return TransactionBehavior switch
        {
            UnitOfWorkTransactionBehavior.Enabled => true,
            UnitOfWorkTransactionBehavior.Disabled => false,
            UnitOfWorkTransactionBehavior.Auto => autoValue,
            _ => throw new XiHanException("未实现的事务行为：" + TransactionBehavior)
        };
    }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    internal XiHanUnitOfWorkOptions Normalize(XiHanUnitOfWorkOptions options)
    {
        options.IsolationLevel ??= IsolationLevel;

        options.Timeout ??= Timeout;

        return options;
    }
}
