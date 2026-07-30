// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Templating.Security;

/// <summary>
/// 安全威胁类型
/// </summary>
public enum SecurityThreatType
{
    /// <summary>
    /// 文件访问
    /// </summary>
    FileAccess,

    /// <summary>
    /// 网络访问
    /// </summary>
    NetworkAccess,

    /// <summary>
    /// 反射操作
    /// </summary>
    Reflection,

    /// <summary>
    /// 类型实例化
    /// </summary>
    TypeInstantiation,

    /// <summary>
    /// 危险方法调用
    /// </summary>
    DangerousMethodCall,

    /// <summary>
    /// 无限循环风险
    /// </summary>
    InfiniteLoopRisk,

    /// <summary>
    /// 内存耗尽风险
    /// </summary>
    MemoryExhaustionRisk,

    /// <summary>
    /// 代码注入
    /// </summary>
    CodeInjection,

    /// <summary>
    /// 路径遍历
    /// </summary>
    PathTraversal,

    /// <summary>
    /// 敏感信息泄露
    /// </summary>
    InformationDisclosure
}
