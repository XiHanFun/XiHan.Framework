// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Packaging;

/// <summary>
/// 要生成的产物种类。
/// </summary>
[Flags]
internal enum PackKinds
{
    /// <summary>无。</summary>
    None = 0,

    /// <summary>Dockerfile 与 .dockerignore。</summary>
    Dockerfile = 1,

    /// <summary>Kustomize 清单。</summary>
    Kustomize = 2,

    /// <summary>Compose 文件。</summary>
    Compose = 4,

    /// <summary>Aspire AppHost 登记代码。</summary>
    Aspire = 8,

    /// <summary>全部。</summary>
    All = Dockerfile | Kustomize | Compose | Aspire,
}
