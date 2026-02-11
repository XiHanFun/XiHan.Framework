#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanUnitOfWorkOptions
// Guid:410954b7-3cf1-4857-bcd0-7d61f6e0664e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 06:32:37
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Data;

namespace XiHan.Framework.Uow.Options;

/// <summary>
/// 工作单元选项
/// </summary>
public class XiHanUnitOfWorkOptions : IXiHanUnitOfWorkOptions
{
    /// <summary>
    /// 默认构造函数
    /// </summary>
    public XiHanUnitOfWorkOptions()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isTransactional"></param>
    /// <param name="isolationLevel"></param>
    /// <param name="timeout"></param>
    public XiHanUnitOfWorkOptions(bool isTransactional = false, IsolationLevel? isolationLevel = null, int? timeout = null)
    {
        IsTransactional = isTransactional;
        IsolationLevel = isolationLevel;
        Timeout = timeout;
    }

    /// <summary>
    /// 是否事务性
    /// </summary>
    public bool IsTransactional { get; set; }

    /// <summary>
    /// 隔离级别
    /// </summary>
    public IsolationLevel? IsolationLevel { get; set; }

    /// <summary>
    /// 超时时间 (单位：毫秒)
    /// </summary>
    public int? Timeout { get; set; }

    /// <summary>
    /// 克隆
    /// </summary>
    /// <returns></returns>
    public XiHanUnitOfWorkOptions Clone()
    {
        return new XiHanUnitOfWorkOptions
        {
            IsTransactional = IsTransactional,
            IsolationLevel = IsolationLevel,
            Timeout = Timeout
        };
    }
}
