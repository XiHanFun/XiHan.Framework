#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IOnServiceActivatedContext
// Guid:9b4d06c0-ad70-4959-898b-ded17612891d
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/27 21:58:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务激活上下文接口
/// </summary>
public interface IOnServiceActivatedContext
{
    /// <summary>
    /// 实例
    /// </summary>
    public object Instance { get; }
}
