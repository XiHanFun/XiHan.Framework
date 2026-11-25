// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Hosting;

/// <summary>
/// 一个绑定目录超过的是哪一个上限
/// </summary>
/// <remarks>
/// RFC-0016「资源绑定与配置节」第 9、10 条（ADR-0292、ADR-0301）；插件宿主据此写出配置失败的固定 <c>reason</c>
/// </remarks>
internal enum BindingLimit
{
    /// <summary>
    /// 一个文件超过 <see cref="BindingResolver.MaxFileBytes"/> 字节
    /// </summary>
    FileBytes,

    /// <summary>
    /// 读的文件合计超过 <see cref="BindingResolver.MaxBindingBytes"/> 字节
    /// </summary>
    TotalBytes,

    /// <summary>
    /// 读的文件多于 <see cref="BindingResolver.MaxBindingFiles"/> 个
    /// </summary>
    FileCount,
}
