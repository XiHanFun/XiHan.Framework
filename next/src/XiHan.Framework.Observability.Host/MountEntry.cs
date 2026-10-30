// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Observability.Host;

/// <summary>
/// 挂载表的一行。
/// </summary>
/// <param name="Device">设备，如 <c>/dev/sda1</c>。</param>
/// <param name="MountPoint">挂载点。</param>
/// <param name="FileSystem">文件系统，如 <c>ext4</c>。</param>
internal readonly record struct MountEntry(string Device, string MountPoint, string FileSystem);
