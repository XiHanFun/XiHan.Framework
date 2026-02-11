#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:GoErrorTemplates
// Guid:6d7e8f9a-1b2c-3d4e-5f6a-7b8c9d0e1f2a
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

/// <summary>
/// Go 错误模板
/// </summary>
internal static class GoErrorTemplates
{
    /// <summary>
    /// Go 错误模板集合
    /// </summary>
    public static readonly string[] Templates =
    [
        "panic: runtime error: invalid memory address or nil pointer dereference\n[signal SIGSEGV: segmentation violation code=0x1 addr=0x0 pc=0x4a2e3b]\n\ngoroutine 1 [running]:\nmain.processRequest(0x0, 0x0)\n\t/app/handlers/request.go:45 +0x2b\nmain.main()\n\t/app/main.go:23 +0x56",
        "panic: runtime error: index out of range [5] with length 3\n\ngoroutine 12 [running]:\nmain.(*DataProcessor).Process(0xc000010180, 0xc000096000, 0x3, 0x3)\n\t/app/services/processor.go:78 +0x1c5\nmain.handleData(0xc000096000, 0x3, 0x3)\n\t/app/handlers/data.go:34 +0x89",
        "fatal error: concurrent map writes\n\ngoroutine 45 [running]:\nruntime.throw(0x7f8a3c, 0x15)\n\t/usr/local/go/src/runtime/panic.go:1117 +0x72\nmain.(*Cache).Set(...)\n\t/app/cache/memory.go:89\nmain.storeData()\n\t/app/services/storage.go:56 +0x12d",
        "panic: sql: database is closed\n\ngoroutine 23 [running]:\ndatabase/sql.(*DB).conn(0xc0000a8000, 0x8f3ec0, 0xc0000b2000, 0x1, 0x0, 0x0, 0x0)\n\t/usr/local/go/src/database/sql/sql.go:1206 +0x1c9\nmain.queryUsers()\n\t/app/repository/user.go:67 +0x234",
        "panic: http: panic serving 192.168.1.100:52341: template: index.html:15:23: executing \"index.html\" at <.User.Name>: nil pointer evaluating interface {}.Name\n\ngoroutine 34 [running]:\nnet/http.(*conn).serve.func1(0xc000108000)\n\t/usr/local/go/src/net/http/server.go:1801 +0x125",
        "panic: assignment to entry in nil map\n\ngoroutine 8 [running]:\nmain.(*Config).LoadSettings(0xc000010240, 0xc000086000, 0x5, 0x8)\n\t/app/config/loader.go:92 +0x15f\nmain.initialize()\n\t/app/main.go:34 +0x67",
        "fatal error: all goroutines are asleep - deadlock!\n\ngoroutine 1 [chan receive]:\nmain.processQueue(0xc00007e000)\n\t/app/queue/processor.go:45 +0x85\nmain.main()\n\t/app/main.go:28 +0x125",
        "panic: close of closed channel\n\ngoroutine 15 [running]:\nmain.(*Worker).Stop(0xc0000b6000)\n\t/app/worker/worker.go:123 +0x56\nmain.shutdown()\n\t/app/main.go:89 +0x234",
        "panic: interface conversion: interface {} is nil, not string\n\ngoroutine 7 [running]:\nmain.processConfig(0xc000092000)\n\t/app/config/parser.go:156 +0x3c4\nmain.loadConfiguration()\n\t/app/main.go:67 +0x89",
        "fatal error: runtime: out of memory\n\nruntime stack:\nruntime.throw(0x7ffabc, 0x16)\n\t/usr/local/go/src/runtime/panic.go:1117 +0x72\nruntime.sysMap(0xc040000000, 0x4000000, 0x11d5118)\n\t/usr/local/go/src/runtime/mem_linux.go:169 +0xc5"
    ];
}
