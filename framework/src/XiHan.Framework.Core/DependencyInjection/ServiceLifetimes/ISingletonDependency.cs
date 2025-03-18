﻿#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ISingletonDependency
// Guid:db43aa3e-99c7-450e-8238-f48ecab29387
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 1:04:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.CodeAnalysis.Attributes;

namespace XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

/// <summary>
/// 依赖关系注入生命周期，单例依赖接口
/// </summary>
[ClassOnlyInterface]
public interface ISingletonDependency;
