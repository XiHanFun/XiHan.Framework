#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IAmbientUnitOfWork
// Guid:647ce176-4996-4538-952e-a190cf425324
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/6/2 5:11:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow;

/// <summary>
/// 环境工作单元接口
/// </summary>
public interface IAmbientUnitOfWork : IUnitOfWorkAccessor
{
    /// <summary>
    /// 获取当前工作单元
    /// </summary>
    /// <returns></returns>
    IUnitOfWork? GetCurrentByChecking();
}
