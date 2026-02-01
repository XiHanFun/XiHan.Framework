#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:SecurityThreatType
// Guid:35ba0611-7270-4253-bf60-458112aaa532
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/09/23 04:03:03
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

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
