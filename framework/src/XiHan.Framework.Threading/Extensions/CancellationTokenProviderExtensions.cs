// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Threading.Extensions;

/// <summary>
/// 令牌提供者的扩展方法
/// </summary>
public static class CancellationTokenProviderExtensions
{
    /// <summary>
    /// 回滚到提供者
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="prefferedValue"></param>
    /// <returns></returns>
    public static CancellationToken FallbackToProvider(this ICancellationTokenProvider provider, CancellationToken prefferedValue = default)
    {
        return prefferedValue == CancellationToken.None || prefferedValue == CancellationToken.None
            ? provider.Token
            : prefferedValue;
    }
}
