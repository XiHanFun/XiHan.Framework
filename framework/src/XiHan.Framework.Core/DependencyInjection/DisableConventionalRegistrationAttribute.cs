#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:DisableConventionalRegistrationAttribute
// Guid:a9f78139-392f-4f6a-92e6-e3562afee91c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:29:09
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Core.DependencyInjection;

/// <summary>
/// 禁止常规注册特性
/// </summary>
[AttributeUsage(AttributeTargets.All)]
public class DisableConventionalRegistrationAttribute : Attribute
{
}