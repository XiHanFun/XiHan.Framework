#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2021-Present ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:NodeJsErrorTemplates
// Guid:8f9a0b1c-3d4e-5f6a-7b8c-9d0e1f2a3b4c
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2026/01/29 00:00:00
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Security.ErrorObfuscation.Templates;

/// <summary>
/// Node.js 错误模板
/// </summary>
internal static class NodeJsErrorTemplates
{
    /// <summary>
    /// Node.js 错误模板集合
    /// </summary>
    public static readonly string[] Templates =
    [
        "TypeError: Cannot read property 'name' of undefined\n    at processUser (/app/controllers/userController.js:45:23)\n    at Layer.handle [as handle_request] (/app/node_modules/express/lib/router/layer.js:95:5)\n    at next (/app/node_modules/express/lib/router/route.js:137:13)\n    at Route.dispatch (/app/node_modules/express/lib/router/route.js:112:3)",
        "Error: connect ECONNREFUSED 127.0.0.1:3306\n    at TCPConnectWrap.afterConnect [as oncomplete] (node:net:1494:16)\n    at Protocol._enqueue (/app/node_modules/mysql/lib/protocol/Protocol.js:144:48)\n    at Connection.connect (/app/node_modules/mysql/lib/Connection.js:119:18)\n    at Object.createConnection (/app/services/database.js:23:10)",
        "ReferenceError: userData is not defined\n    at /app/routes/api.js:67:15\n    at Layer.handle [as handle_request] (/app/node_modules/express/lib/router/layer.js:95:5)\n    at next (/app/node_modules/express/lib/router/route.js:137:13)",
        "SyntaxError: Unexpected token } in JSON at position 145\n    at JSON.parse (<anonymous>)\n    at IncomingMessage.<anonymous> (/app/middleware/parser.js:34:23)\n    at IncomingMessage.emit (node:events:525:35)",
        "Error: ENOENT: no such file or directory, open '/app/config/settings.json'\n    at Object.openSync (node:fs:590:3)\n    at Object.readFileSync (node:fs:458:35)\n    at loadConfig (/app/utils/config.js:15:20)\n    at Object.<anonymous> (/app/app.js:8:1)",
        "MongoError: connection 0 to localhost:27017 closed\n    at Connection.<anonymous> (/app/node_modules/mongodb/lib/core/connection/connection.js:259:9)\n    at Connection.emit (node:events:525:35)\n    at Socket.<anonymous> (/app/node_modules/mongodb/lib/core/connection/connection.js:229:12)",
        "RangeError: Maximum call stack size exceeded\n    at calculateRecursive (/app/utils/math.js:23:3)\n    at calculateRecursive (/app/utils/math.js:24:12)\n    at calculateRecursive (/app/utils/math.js:24:12)",
        "UnhandledPromiseRejectionWarning: Error: Request failed with status code 500\n    at createError (/app/node_modules/axios/lib/core/createError.js:16:15)\n    at settle (/app/node_modules/axios/lib/core/settle.js:17:12)\n    at IncomingMessage.handleStreamEnd (/app/node_modules/axios/lib/adapters/http.js:269:11)",
        "Error: listen EADDRINUSE: address already in use :::3000\n    at Server.setupListenHandle [as _listen2] (node:net:1463:16)\n    at listenInCluster (node:net:1511:12)\n    at Server.listen (node:net:1599:7)\n    at Function.listen (/app/node_modules/express/lib/application.js:618:24)",
        "TypeError: Converting circular structure to JSON\n    at JSON.stringify (<anonymous>)\n    at stringifyResponse (/app/middleware/response.js:56:18)\n    at Layer.handle [as handle_request] (/app/node_modules/express/lib/router/layer.js:95:5)"
    ];
}
