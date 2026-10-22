; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
XHG001  | XiHan    | Error    | Operation classes must implement IOperation<TRequest, TResponse>
XHG002  | XiHan    | Error    | Operation request and response types must be registered in a JsonSerializerContext
XHG003  | XiHan    | Error    | Operation IDs must follow the operation-id grammar
