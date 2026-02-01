#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CancellationTokenOverride
// Guid:09263cf0-414f-47c1-bbcb-78ed00c789db
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 06:02:08
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Threading;

/// <summary>
/// 令牌重写
/// </summary>
public class CancellationTokenOverride
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="cancellationToken"></param>
    public CancellationTokenOverride(CancellationToken cancellationToken)
    {
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// 令牌
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
