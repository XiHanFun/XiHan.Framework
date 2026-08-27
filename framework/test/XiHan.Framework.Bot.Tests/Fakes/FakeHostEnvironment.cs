// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Bot.Tests;

/// <summary>
/// 手写的 <see cref="IHostEnvironment"/> 替身
/// </summary>
public sealed class FakeHostEnvironment : IHostEnvironment
{
    /// <summary>
    /// 创建替身
    /// </summary>
    /// <param name="environmentName">环境名称</param>
    public FakeHostEnvironment(string environmentName)
    {
        EnvironmentName = environmentName;
    }

    /// <summary>
    /// 环境名称
    /// </summary>
    public string EnvironmentName { get; set; }

    /// <summary>
    /// 应用名称
    /// </summary>
    public string ApplicationName { get; set; } = "XiHan.Framework.Bot.Tests";

    /// <summary>
    /// 内容根路径
    /// </summary>
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// 内容根文件提供程序
    /// </summary>
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
