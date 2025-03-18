﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ITransientDependency
// Guid:4d5ce785-7cdd-4fae-befd-494afb9fd55e
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:04:35
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.CodeAnalysis.Attributes;

namespace XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

/// <summary>
/// 依赖关系注入生命周期，瞬态依赖接口
/// </summary>
[ClassOnlyInterface]
public interface ITransientDependency;
