#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:BottleneckType
// Guid:a048d187-c39b-4da6-8120-cba026d6c188
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/9/23 4:20:02
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Templating.Compilers;

/// <summary>
/// 瓶颈类型
/// </summary>
public enum BottleneckType
{
    /// <summary>
    /// 复杂表达式
    /// </summary>
    ComplexExpression,

    /// <summary>
    /// 嵌套循环
    /// </summary>
    NestedLoop,

    /// <summary>
    /// 大量字符串连接
    /// </summary>
    StringConcatenation,

    /// <summary>
    /// 频繁反射调用
    /// </summary>
    FrequentReflection,

    /// <summary>
    /// 未缓存的计算
    /// </summary>
    UncachedComputation,

    /// <summary>
    /// 大对象分配
    /// </summary>
    LargeObjectAllocation
}
