// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Authorization.Conditions;

/// <summary>
/// 属性名的解析结果
/// </summary>
/// <param name="CanonicalName">规范名：精确登记取登记名，别名取所指登记名，族取原名（不带通配）</param>
/// <param name="Descriptor">登记项</param>
/// <param name="IsAlias">是否经旧名别名解析</param>
public sealed record ConditionAttributeResolution(string CanonicalName, ConditionAttributeDescriptor Descriptor, bool IsAlias);
