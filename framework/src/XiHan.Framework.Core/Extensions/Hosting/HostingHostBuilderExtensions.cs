#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:HostingHostBuilderExtensions
// Guid:f5d9b294-41e3-4329-ac72-b3370504f6d8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/10/28 2:46:59
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Core.Extensions.Hosting;

/// <summary>
/// 主机构建器扩展方法
/// </summary>
public static class HostingHostBuilderExtensions
{
    /// <summary>
    /// 应用私密信息设置 JSON 路径
    /// </summary>
    public const string AppSettingsSecretJsonPath = "appsettings.secrets.json";

    /// <summary>
    /// 添加应用设置的私密 JSON
    /// </summary>
    /// <param name="hostBuilder"></param>
    /// <param name="optional"></param>
    /// <param name="reloadOnChange"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static IHostBuilder AddAppSettingsSecretsJson(
        this IHostBuilder hostBuilder,
        bool optional = true,
        bool reloadOnChange = true,
        string path = AppSettingsSecretJsonPath)
    {
        return hostBuilder.ConfigureAppConfiguration((_, builder) =>
        {
            builder.AddJsonFile(path: path, optional: optional, reloadOnChange: reloadOnChange);
        });
    }
}