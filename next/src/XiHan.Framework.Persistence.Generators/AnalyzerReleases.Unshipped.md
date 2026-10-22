; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
XHP001  | XiHan    | Error    | Entity classes must implement IEntity<TKey> with a single key type
XHP002  | XiHan    | Error    | Entity classes must be top-level public or internal classes
XHP003  | XiHan    | Warning  | Generated filter class name conflicts with an existing type
