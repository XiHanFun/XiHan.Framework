#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NullDisposable
// Guid:257546a7-6db3-4fc5-b7be-d143816958f1
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 2:14:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Core;

/// <summary>
/// 空可释放对象
/// </summary>
public sealed class NullDisposable : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    private NullDisposable()
    {
    }

    /// <summary>
    /// 实例
    /// </summary>
    public static NullDisposable Instance { get; } = new();

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
    }
}
