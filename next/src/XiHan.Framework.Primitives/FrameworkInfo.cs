// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives;

/// <summary>
/// 框架身份信息。版本由构建期生成的常量提供，不经反射读取。
/// </summary>
[ApiLevel(Stability.Stable, "5.0")]
public static class FrameworkInfo
{
    /// <summary>
    /// 框架名称。
    /// </summary>
    public const string Name = "XiHan Framework";

    /// <summary>
    /// 实现的 XiHan Spec 大版本。
    /// </summary>
    public const string SpecVersion = "1";

    /// <summary>
    /// 框架版本（与包版本一致）。
    /// </summary>
    public static string Version => GeneratedVersion.Value;

    /// <summary>
    /// 源码仓库。
    /// </summary>
    public static Uri Repository { get; } = new("https://github.com/XiHanFun/XiHan.Framework");

    /// <summary>
    /// 文档站。
    /// </summary>
    public static Uri Documentation { get; } = new("https://framework.docs.xihanfun.com");
}
