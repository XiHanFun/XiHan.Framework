#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:OnServiceActivatedContext
// Guid:8215ad5f-5ff4-4c63-aa5c-aaaf94fc65af
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/27 22:30:13
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务激活时的上下文
/// </summary>
public class OnServiceActivatedContext : IOnServiceActivatedContext
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="instance"></param>
    public OnServiceActivatedContext(object instance)
    {
        Instance = instance;
    }

    /// <summary>
    /// 服务实例
    /// </summary>
    public object Instance { get; }
}
