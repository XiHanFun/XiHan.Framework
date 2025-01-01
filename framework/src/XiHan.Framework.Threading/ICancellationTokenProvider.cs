#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ICancellationTokenProvider
// Guid:6279b378-520c-46be-a36c-f1d83f0c9f67
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 5:56:36
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Threading;

/// <summary>
/// 令牌提供者接口
/// </summary>
public interface ICancellationTokenProvider
{
    /// <summary>
    /// 令牌
    /// </summary>
    CancellationToken Token { get; }

    /// <summary>
    /// 使用
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    IDisposable Use(CancellationToken cancellationToken);
}
