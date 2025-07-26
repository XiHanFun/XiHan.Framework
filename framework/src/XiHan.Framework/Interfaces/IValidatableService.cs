#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:IValidatableService
// Guid:0bb4ba7a-182a-4a74-aee2-20eed339b5e7
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2025/3/17 13:56:10
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Interfaces;

/// <summary>
/// 可验证服务接口
/// </summary>
public interface IValidatableService : IFrameworkService
{
    /// <summary>
    /// 验证配置
    /// </summary>
    /// <returns>验证结果</returns>
    Task<bool> ValidateConfigurationAsync();

    /// <summary>
    /// 获取验证错误
    /// </summary>
    /// <returns>验证错误</returns>
    Task<IEnumerable<string>> GetValidationErrorsAsync();
}
