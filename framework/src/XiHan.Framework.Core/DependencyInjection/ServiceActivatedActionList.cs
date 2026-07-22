// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
