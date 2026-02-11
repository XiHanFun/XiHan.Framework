#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullAsyncDisposable
// Guid:395a3a47-c376-4f77-b47b-39f20cbf74bf
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 04:08:20
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Threading;

/// <summary>
/// 空可异步释放对象
/// </summary>
public sealed class NullAsyncDisposable : IAsyncDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    private NullAsyncDisposable()
    {
    }

    /// <summary>
    /// 实例
    /// </summary>
    public static NullAsyncDisposable Instance { get; } = new();

    /// <summary>
    /// 释放资源
    /// </summary>
    /// <returns></returns>
    public ValueTask DisposeAsync()
    {
        return default;
    }
}
