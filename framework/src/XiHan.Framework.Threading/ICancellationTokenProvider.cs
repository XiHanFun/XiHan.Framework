// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
