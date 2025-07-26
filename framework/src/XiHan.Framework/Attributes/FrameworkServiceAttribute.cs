#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:FrameworkServiceAttribute
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5f0
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Attributes;

/// <summary>
/// 框架服务特性
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class FrameworkServiceAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="serviceName">服务名称</param>
    /// <param name="description">服务描述</param>
    /// <param name="version">服务版本</param>
    /// <param name="isSingleton">是否单例</param>
    /// <param name="initializationOrder">初始化顺序</param>
    public FrameworkServiceAttribute(string serviceName, string description = "", string version = "1.0.0", bool isSingleton = true, int initializationOrder = 0)
    {
        ServiceName = serviceName;
        Description = description;
        Version = version;
        IsSingleton = isSingleton;
        InitializationOrder = initializationOrder;
    }

    /// <summary>
    /// 服务名称
    /// </summary>
    public string ServiceName { get; }

    /// <summary>
    /// 服务描述
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// 服务版本
    /// </summary>
    public string Version { get; }

    /// <summary>
    /// 是否单例
    /// </summary>
    public bool IsSingleton { get; }

    /// <summary>
    /// 初始化顺序
    /// </summary>
    public int InitializationOrder { get; }
}
