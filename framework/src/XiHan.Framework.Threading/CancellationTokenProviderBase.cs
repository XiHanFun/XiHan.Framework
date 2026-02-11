#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:CancellationTokenProviderBase
// Guid:d8cc6338-9d9f-46b7-8f96-20eb97ac21ee
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/14 06:00:06
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Threading;

/// <summary>
/// 令牌提供者基类
/// </summary>
public abstract class CancellationTokenProviderBase : ICancellationTokenProvider
{
    /// <summary>
    /// 令牌重写上下文键
    /// </summary>
    public const string CancellationTokenOverrideContextKey = "XiHan.Framework.Threading.CancellationToken.Override";

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="cancellationTokenOverrideScopeProvider"></param>
    protected CancellationTokenProviderBase(IAmbientScopeProvider<CancellationTokenOverride> cancellationTokenOverrideScopeProvider)
    {
        CancellationTokenOverrideScopeProvider = cancellationTokenOverrideScopeProvider;
    }

    /// <summary>
    /// 令牌
    /// </summary>
    public abstract CancellationToken Token { get; }

    /// <summary>
    /// 令牌重写作用域提供者
    /// </summary>
    protected IAmbientScopeProvider<CancellationTokenOverride> CancellationTokenOverrideScopeProvider { get; }

    /// <summary>
    /// 重写值
    /// </summary>
    protected CancellationTokenOverride? OverrideValue => CancellationTokenOverrideScopeProvider.GetValue(CancellationTokenOverrideContextKey);

    /// <summary>
    /// 使用
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public IDisposable Use(CancellationToken cancellationToken)
    {
        return CancellationTokenOverrideScopeProvider.BeginScope(CancellationTokenOverrideContextKey, new CancellationTokenOverride(cancellationToken));
    }
}
