; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
XH0001  | XiHan    | Error    | 公开类型必须标注 ApiLevel
XH0002  | XiHan    | Error    | 公开类型名不得以 XiHan 开头
XH0003  | XiHan    | Error    | 不得在程序集之外使用 Internal 稳定性的 API
XH0004  | XiHan    | Error    | 公开类型名不得使用禁用词
XH0005  | XiHan    | Error    | Ring 0/1 不得使用反射
