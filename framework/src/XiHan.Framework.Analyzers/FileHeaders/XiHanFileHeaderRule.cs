#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:XiHanFileHeaderRule
// Guid:9b2b3f4e-c7ea-4c6f-b1fd-1d836c1c4c91
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/04/26 17:33:31
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

using Microsoft.CodeAnalysis;

namespace XiHan.Framework.Analyzers.FileHeaders;

internal static class XiHanFileHeaderRule
{
    internal const string DiagnosticId = "XHFH001";
    internal const string Category = "XiHan.FileHeader";

    internal static readonly DiagnosticDescriptor Descriptor = new(
        DiagnosticId,
        "缺少曦寒标准版权文件头",
        "文件 '{0}' 缺少或未正确声明曦寒标准版权文件头",
        Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "C# 源文件必须包含曦寒标准版权版本注释，包含 MIT 授权、作者、邮箱、文件名、GUID 与创建时间.");
}
