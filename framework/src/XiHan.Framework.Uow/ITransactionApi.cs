#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITransactionApi
// Guid:af57ecf1-1be6-4206-b13c-c15fea484308
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/16 4:01:52
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Uow;

/// <summary>
/// 事务API
/// </summary>
public interface ITransactionApi : IDisposable
{
    /// <summary>
    /// 异步提交
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
