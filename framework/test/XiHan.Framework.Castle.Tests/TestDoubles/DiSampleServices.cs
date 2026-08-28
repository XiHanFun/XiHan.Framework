// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Castle.Tests.TestDoubles;

/// <summary>
/// 问候服务契约，用于依赖注入装配用例
/// </summary>
public interface IGreetingService
{
    /// <summary>
    /// 问候
    /// </summary>
    /// <param name="name">姓名</param>
    /// <returns>问候语</returns>
    string Greet(string name);
}

/// <summary>
/// 问候服务
/// </summary>
public sealed class GreetingService : IGreetingService
{
    /// <summary>
    /// 问候
    /// </summary>
    /// <param name="name">姓名</param>
    /// <returns>问候语</returns>
    public string Greet(string name)
    {
        return $"你好，{name}";
    }
}

/// <summary>
/// 带标记的服务契约，用于验证工厂/实例注册的目标创建方式
/// </summary>
public interface ITaggedService
{
    /// <summary>
    /// 标记
    /// </summary>
    string Tag { get; }
}

/// <summary>
/// 带标记的服务
/// </summary>
/// <remarks>
/// 只保留有参构造，确保它只可能经工厂或现成实例创建，
/// 从而能反向证明代理没有绕过原描述器自己新建目标。
/// </remarks>
public sealed class TaggedService : ITaggedService
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="tag">标记</param>
    public TaggedService(string tag)
    {
        Tag = tag;
    }

    /// <summary>
    /// 标记
    /// </summary>
    public string Tag { get; }
}

/// <summary>
/// 被动态代理忽略名单排除的服务契约
/// </summary>
public interface IIgnoredService
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    string Ping();
}

/// <summary>
/// 被动态代理忽略名单排除的服务
/// </summary>
/// <remarks>
/// <c>DynamicProxyIgnoreTypes</c> 是进程级静态名单，本类型只在忽略名单用例里登记，
/// 不与其它用例共用，避免污染扩散。
/// </remarks>
public sealed class IgnoredService : IIgnoredService
{
    /// <summary>
    /// 返回固定值
    /// </summary>
    /// <returns>固定字符串</returns>
    public string Ping()
    {
        return "ignored";
    }
}
