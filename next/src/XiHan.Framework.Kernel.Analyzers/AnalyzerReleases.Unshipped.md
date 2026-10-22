; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
XH0001  | XiHan    | Error    | Public types must be annotated with ApiLevel
XH0002  | XiHan    | Error    | Public type names must not start with XiHan
XH0003  | XiHan    | Error    | Internal-stability APIs must not be used outside their own assembly
XH0004  | XiHan    | Error    | Public type names must not use forbidden words
XH0005  | XiHan    | Error    | Ring 0/1 must not use reflection
