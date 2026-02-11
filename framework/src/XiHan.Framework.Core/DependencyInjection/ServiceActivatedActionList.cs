#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ServiceActivatedActionList
// Guid:ecba2f0a-5415-445a-9862-6888aeb55fea
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/04/27 22:33:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.DependencyInjection;

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 服务激活时的动作列表
/// </summary>
public class ServiceActivatedActionList : List<KeyValuePair<ServiceDescriptor, Action<IOnServiceActivatedContext>>>
{
    /// <summary>
    /// 添加服务激活时的动作
    /// </summary>
    /// <param name="descriptor"></param>
    /// <returns></returns>
    public List<Action<IOnServiceActivatedContext>> GetActions(ServiceDescriptor descriptor)
    {
        return [.. this.Where(x => x.Key == descriptor).Select(x => x.Value)];
    }
}
