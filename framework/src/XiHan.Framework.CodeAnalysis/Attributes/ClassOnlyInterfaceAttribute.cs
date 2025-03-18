#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ClassOnlyInterfaceAttribute
// Guid:bdfc9d6a-96c8-4fd4-b963-6d75f7dce540
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/18 20:18:55
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.CodeAnalysis.Attributes;

/// <summary>
/// 仅用于标记接口，表示该接口只能用于类
/// </summary>
[AttributeUsage(AttributeTargets.Interface)]
public class ClassOnlyInterfaceAttribute : Attribute;
