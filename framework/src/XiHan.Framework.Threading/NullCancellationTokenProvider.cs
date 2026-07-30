// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
