// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Security.Abstractions.Conditions;

/// <summary>
/// 属性名的解析结果。
/// </summary>
/// <param name="CanonicalName">规范名：精确登记取登记名，别名取所指登记名，族取原名（不带通配）。</param>
/// <param name="Descriptor">登记项。</param>
/// <param name="IsAlias">是否经旧名别名解析。</param>
[ApiLevel(Stability.Preview, "5.0")]
public sealed record ConditionAttributeResolution(string CanonicalName, ConditionAttributeDescriptor Descriptor, bool IsAlias);
