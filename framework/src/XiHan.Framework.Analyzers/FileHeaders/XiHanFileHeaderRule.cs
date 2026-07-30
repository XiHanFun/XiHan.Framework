// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

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
        description: "C# 源文件必须以标准的两行版权与 MIT 授权声明开头.");
}
