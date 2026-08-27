// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectMapping.Tests.Fakes;

/// <summary>
/// 扩展属性测试用枚举
/// </summary>
/// <remarks>
/// 数值被 ToEnum 的「数字字符串解析」分支依赖，不得随意调整。
/// </remarks>
public enum FakeExtensionEnum
{
    /// <summary>
    /// 未指定
    /// </summary>
    None = 0,

    /// <summary>
    /// 第一项
    /// </summary>
    First = 1,

    /// <summary>
    /// 第二项
    /// </summary>
    Second = 2
}
