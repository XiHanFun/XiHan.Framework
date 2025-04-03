#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullCancellationTokenProvider
// Guid:fb8202b9-56d7-4a29-be68-231988b094b8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 5:58:51
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Threading;

/// <summary>
/// 空令牌提供者
/// </summary>
public class NullCancellationTokenProvider : CancellationTokenProviderBase
{
    /// <summary>
    /// 构造函数
    /// </summary>
    private NullCancellationTokenProvider()
        : base(new AmbientDataContextAmbientScopeProvider<CancellationTokenOverride>(new AsyncLocalAmbientDataContext()))
    {
    }

    /// <summary>
    /// 实例
    /// </summary>
    public static NullCancellationTokenProvider Instance { get; } = new();

    /// <summary>
    /// 令牌
    /// </summary>
    public override CancellationToken Token => OverrideValue?.CancellationToken ?? CancellationToken.None;
}
