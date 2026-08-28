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
            // 原写法是 `_ = (HostBuilderContext)builder.AddJsonFile(...)`：这里的 `_` 是 lambda 的第一个形参
            // （HostBuilderContext），不是弃元，于是那行把 AddJsonFile 返回的 IConfigurationBuilder 强转成
            // HostBuilderContext，运行期必抛 InvalidCastException。委托是延迟执行的，调用扩展本身不炸，
            // 直到 hostBuilder.Build() 才炸，等于任何用了这个扩展的宿主都起不来。
            // 该扩展的职责只是把 JSON 源追加进应用配置，返回值无需接收，更无需强转。
            builder.AddJsonFile(path, optional, reloadOnChange);
        });
    }
}
