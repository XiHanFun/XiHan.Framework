// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
