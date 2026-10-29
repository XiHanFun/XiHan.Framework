// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 镜像构建设置。
/// </summary>
internal sealed record BuildSettings
{
    /// <summary>
    /// Docker 构建上下文，相对清单所在目录。
    /// </summary>
    /// <remarks>
    /// 默认就是清单所在目录。单仓库里引用框架源码的应用把它指向仓库根。
    /// </remarks>
    public string Context { get; set; } = ".";
}
