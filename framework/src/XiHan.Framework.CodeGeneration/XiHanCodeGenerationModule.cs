#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanCodeGenerationModule
// Guid:2a782966-77a5-4f6d-81c1-ae556631805a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/12/6 3:29:57
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using XiHan.Framework.Core.Modularity;
using XiHan.Framework.TextTemplating;

namespace XiHan.Framework.CodeGeneration;

/// <summary>
/// 曦寒框架代码生成模块
/// </summary>
[DependsOn(
    typeof(XiHanTextTemplatingModule)
)]
public class XiHanCodeGenerationModule : XiHanModule
{
    /// <summary>
    /// 服务配置
    /// </summary>
    /// <param name="context"></param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
    }
}
