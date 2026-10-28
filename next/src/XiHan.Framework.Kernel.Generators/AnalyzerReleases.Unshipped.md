; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
XHG001  | XiHan    | Error    | Operation classes must implement IOperation<TRequest, TResponse>
XHG002  | XiHan    | Error    | Operation request and response types must be registered in a JsonSerializerContext
XHG003  | XiHan    | Error    | Operation IDs must follow the operation-id grammar
XHG004  | XiHan    | Disabled | Operations that rely on the implicit Expose.All default (no explicit Expose and no assembly OperationDefaults)
XHG005  | XiHan    | Error    | Write verbs (create, update, delete) cannot be declared read-only
