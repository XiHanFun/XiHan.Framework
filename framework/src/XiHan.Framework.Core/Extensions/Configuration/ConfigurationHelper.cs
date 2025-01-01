#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:ConfigurationHelper
// Guid:73d1eb15-2bcd-4895-8979-b32ebb3e4929
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/27 2:09:17
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using XiHan.Framework.Utils.Text;

namespace XiHan.Framework.Core.Extensions.Configuration;

/// <summary>
/// 配置帮助类
/// </summary>
public static class ConfigurationHelper
{
    /// <summary>
    /// 绑定配置
    /// </summary>
    /// <param name="options"></param>
    /// <param name="builderAction"></param>
    /// <returns></returns>
    public static IConfigurationRoot BuildConfiguration(XiHanConfigurationBuilderOptions? options = null, Action<IConfigurationBuilder>? builderAction = null)
    {
        options ??= new XiHanConfigurationBuilderOptions();

        // 设置基础路径
        if (options.BasePath.IsNullOrEmpty())
        {
            options.BasePath = Directory.GetCurrentDirectory();
        }

        // 加载基础配置文件
        var builder = new ConfigurationBuilder()
            .SetBasePath(options.BasePath!)
            .AddJsonFile(options.FileName + ".json", options.Optional, options.ReloadOnChange)
            .AddJsonFile(options.FileName + ".secrets.json", true, options.ReloadOnChange);

        // 加载特定环境下的配置文件
        if (!options.EnvironmentName.IsNullOrEmpty())
        {
            builder = builder.AddJsonFile($"{options.FileName}.{options.EnvironmentName}.json", true, options.ReloadOnChange);
        }

        // 开发环境，加载用户机密
        if (options.EnvironmentName == "Development")
        {
            if (options.UserSecretsId != null)
            {
                _ = builder.AddUserSecrets(options.UserSecretsId);
            }
            else if (options.UserSecretsAssembly != null)
            {
                _ = builder.AddUserSecrets(options.UserSecretsAssembly, true);
            }
        }

        builder = builder.AddEnvironmentVariables(options.EnvironmentVariablesPrefix);

        if (options.CommandLineArgs != null)
        {
            builder = builder.AddCommandLine(options.CommandLineArgs);
        }

        builderAction?.Invoke(builder);

        return builder.Build();
    }
}
