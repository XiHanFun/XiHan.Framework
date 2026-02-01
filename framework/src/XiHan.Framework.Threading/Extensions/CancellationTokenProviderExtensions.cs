#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CancellationTokenProviderExtensions
// Guid:54db9044-3e02-471a-8fa1-b98bc16bae80
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 06:10:22
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
