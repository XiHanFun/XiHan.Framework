// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.MultiTenancy.Abstractions;

/// <summary>
/// 当前租户接口
/// 定义获取和管理当前请求上下文中租户信息的契约
/// 提供租户标识、名称以及临时切换租户上下文的功能
/// </summary>
public interface ICurrentTenant
{
    /// <summary>
    /// 获取当前是否处于某个业务租户
    /// 平台就是 0 号租户：无租户上下文与 0 号租户都视为平台，不算可用的业务租户
    /// </summary>
    /// <value>当前处于业务租户（标识大于 0）时为 true，平台时为 false</value>
    bool IsAvailable { get; }

    /// <summary>
    /// 获取当前租户的唯一标识符
    /// </summary>
    /// <value>租户的全局唯一标识；平台可能为 null 或 0，二者同义</value>
    long? Id { get; }

    /// <summary>
    /// 获取当前租户名称
    /// </summary>
    /// <value>租户的显示名称，如果当前没有租户或未设置名称则为 null</value>
    string? Name { get; }

    /// <summary>
    /// 临时更改当前租户信息
    /// 创建一个作用域，在该作用域内临时切换到指定的租户上下文
    /// 当返回的 IDisposable 对象被释放时，租户上下文将恢复到之前的状态
    /// </summary>
    /// <param name="id">要切换到的租户唯一标识，传入 null 表示切换到无租户状态</param>
    /// <param name="name">租户名称，可选参数</param>
    /// <returns>用于恢复租户上下文的释放器对象</returns>
    /// <remarks>
    /// 使用 using 语句确保在作用域结束时正确恢复租户上下文：
    /// <code>
    /// using (currentTenant.Change(tenantId, "TenantName"))
    /// {
    ///     // 在此作用域内，当前租户被临时设置为指定租户
    /// }
    /// // 作用域结束后，租户上下文自动恢复
    /// </code>
    /// </remarks>
    IDisposable Change(long? id, string? name = null);
}
