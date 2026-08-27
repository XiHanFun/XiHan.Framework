// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.ComponentModel;
using XiHan.Framework.Utils.Enums;

namespace XiHan.Framework.Localization.Tests.TestSupport;

/// <summary>
/// 枚举本地化用例的订单状态枚举
/// </summary>
/// <remarks>
/// 短名称刻意加 Localization 前缀：EnumLocalizationService 会扫描全部已加载程序集并按短名建索引，
/// 短名冲突的枚举会被整体剔除，测试枚举必须在整个进程范围内唯一。
/// </remarks>
public enum LocalizationTestOrderStatus
{
    /// <summary>
    /// 待处理
    /// </summary>
    [EnumDisplay("待处理原始描述")]
    Pending = 0,

    /// <summary>
    /// 已支付
    /// </summary>
    [Description("已支付原始描述")]
    Paid = 1,

    /// <summary>
    /// 已发货（无描述特性，描述降级为字段名）
    /// </summary>
    Shipped = 2,

    /// <summary>
    /// 已取消
    /// </summary>
    [EnumDisplay("已取消原始描述")]
    Cancelled = 3
}

/// <summary>
/// 枚举本地化用例的店铺状态枚举（带类型级资源名与键前缀）
/// </summary>
[EnumLocalizationResource("LocalizationTestShopResource", KeyPrefix = "Shop")]
public enum LocalizationTestShopStatus
{
    /// <summary>
    /// 营业中
    /// </summary>
    Open = 1,

    /// <summary>
    /// 已打烊
    /// </summary>
    Closed = 2
}

/// <summary>
/// 枚举本地化用例的草稿状态枚举（完全无特性，用于验证选项级键前缀）
/// </summary>
public enum LocalizationTestPlainState
{
    /// <summary>
    /// 草稿
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 已发布
    /// </summary>
    Published = 1
}

/// <summary>
/// 枚举本地化用例的可见性枚举（覆盖排序、隐藏、禁用、主题、图标）
/// </summary>
public enum LocalizationTestVisibility
{
    /// <summary>
    /// 次要项
    /// </summary>
    [EnumOrder(2)]
    [EnumTheme("warning")]
    [EnumIcon("eye")]
    Beta = 1,

    /// <summary>
    /// 首要项
    /// </summary>
    [EnumOrder(1)]
    Alpha = 2,

    /// <summary>
    /// 隐藏项
    /// </summary>
    [EnumHidden]
    Gamma = 3,

    /// <summary>
    /// 禁用项
    /// </summary>
    [EnumDisabled]
    Delta = 4
}

/// <summary>
/// 枚举本地化用例的标志位枚举
/// </summary>
[Flags]
[Description("测试权限集合")]
public enum LocalizationTestPermission
{
    /// <summary>
    /// 无
    /// </summary>
    None = 0,

    /// <summary>
    /// 读
    /// </summary>
    Read = 1,

    /// <summary>
    /// 写
    /// </summary>
    Write = 2
}
