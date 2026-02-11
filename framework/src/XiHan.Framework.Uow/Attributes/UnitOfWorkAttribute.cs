#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:UnitOfWorkAttribute
// Guid:943790b5-0351-4ed8-9422-47e2f240578c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/04/01 20:25:04
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using System.Data;
using XiHan.Framework.Uow.Options;

namespace XiHan.Framework.Uow.Attributes;

/// <summary>
/// 工作单元特性
/// 用于指示声明的方法(或类中的所有方法)是原子性的，并应被视为一个工作单元(UOW)
/// </summary>
/// <remarks>
/// 如果在调用此方法之前已经存在工作单元，则此属性无效
/// 在此情况下，它将使用当前的工作单元
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Interface)]
public class UnitOfWorkAttribute : Attribute
{
    /// <summary>
    /// 默认构造函数
    /// </summary>
    public UnitOfWorkAttribute()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isTransactional"></param>
    public UnitOfWorkAttribute(bool isTransactional)
    {
        IsTransactional = isTransactional;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isTransactional"></param>
    /// <param name="isolationLevel"></param>
    public UnitOfWorkAttribute(bool isTransactional, IsolationLevel isolationLevel)
    {
        IsTransactional = isTransactional;
        IsolationLevel = isolationLevel;
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="isTransactional"></param>
    /// <param name="isolationLevel"></param>
    /// <param name="timeout"></param>
    public UnitOfWorkAttribute(bool isTransactional, IsolationLevel isolationLevel, int timeout)
    {
        IsTransactional = isTransactional;
        IsolationLevel = isolationLevel;
        Timeout = timeout;
    }

    /// <summary>
    /// 此 UOW 是否为事务性？
    /// 若未提供，则使用默认值
    /// </summary>
    public bool? IsTransactional { get; set; }

    /// <summary>
    /// 单位为毫秒的 UOW 超时时间
    /// 若未提供，则使用默认值
    /// </summary>
    public int? Timeout { get; set; }

    /// <summary>
    /// 如果此 UOW 是事务性的，此选项表示事务的隔离级别
    /// 如果未提供，则使用默认值
    /// </summary>
    public IsolationLevel? IsolationLevel { get; set; }

    /// <summary>
    /// 用于防止为方法启动工作单元
    /// 如果已启动工作单元，则忽略此属性
    /// 默认值：false
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// 设置选项
    /// </summary>
    /// <param name="options"></param>
    public virtual void SetOptions(XiHanUnitOfWorkOptions options)
    {
        if (IsTransactional.HasValue)
        {
            options.IsTransactional = IsTransactional.Value;
        }

        if (Timeout.HasValue)
        {
            options.Timeout = Timeout;
        }

        if (IsolationLevel.HasValue)
        {
            options.IsolationLevel = IsolationLevel;
        }
    }
}
