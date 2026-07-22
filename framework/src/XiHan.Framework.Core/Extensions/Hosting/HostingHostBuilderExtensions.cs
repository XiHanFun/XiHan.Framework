// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
            _ = (HostBuilderContext)builder.AddJsonFile(path, optional, reloadOnChange);
        });
    }
}
