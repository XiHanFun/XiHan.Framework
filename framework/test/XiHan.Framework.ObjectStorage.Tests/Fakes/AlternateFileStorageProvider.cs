// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectStorage.Tests.Fakes;

/// <summary>
/// 第二个文件存储提供程序替身
/// </summary>
/// <remarks>
/// 仅换一个 ProviderName，用于验证「同一容器内多 Provider 并存、按名解析互不串台」。
/// </remarks>
public class AlternateFileStorageProvider : RecordingFileStorageProvider
{
    /// <summary>
    /// 存储类型名称
    /// </summary>
    public override string ProviderName => "Alternate";
}
