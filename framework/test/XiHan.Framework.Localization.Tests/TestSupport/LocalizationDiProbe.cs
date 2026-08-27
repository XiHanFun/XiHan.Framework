// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.Localization.Tests.TestSupport;

/// <summary>
/// 依赖注入用例的资源探针类型
/// </summary>
/// <remarks>
/// 工厂按 <c>resourceSource.Name</c> 取 JSON 资源名，所以这个类型名就是资源文件里的资源名。
/// </remarks>
public sealed class LocalizationDiProbe
{
}

/// <summary>
/// 依赖注入用例的第二个资源探针类型，用于验证不同类型得到不同的本地化器
/// </summary>
public sealed class LocalizationDiOtherProbe
{
}
