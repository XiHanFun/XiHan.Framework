// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 应用名与环境名固定的宿主环境
/// </summary>
/// <remarks>
/// 内核测试不引用具体宿主，Banner 用例用它代替通用宿主登记的 <see cref="IHostEnvironment"/>
/// </remarks>
/// <param name="applicationName">应用名</param>
/// <param name="environmentName">环境名</param>
public sealed class FixedHostEnvironment(string applicationName, string environmentName) : IHostEnvironment
{
    /// <inheritdoc />
    public string ApplicationName { get; set; } = applicationName;

    /// <inheritdoc />
    public string EnvironmentName { get; set; } = environmentName;

    /// <inheritdoc />
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <inheritdoc />
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
