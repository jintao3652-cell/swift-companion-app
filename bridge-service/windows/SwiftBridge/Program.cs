using SwiftBridge;

Console.WriteLine("===========================================");
Console.WriteLine("swift Companion Bridge (DBus mode)");
Console.WriteLine("===========================================");
Console.WriteLine("swift 启动要求：swiftLauncher 选择「GUI 和 Core（分布式）」，");
Console.WriteLine("或 swiftcore.exe --dbus tcp:host=127.0.0.1,port=45000");
Console.WriteLine("===========================================");

var app = BridgeHost.BuildApp(args: args);
if (app is null)
{
    Console.WriteLine("配置无效（检查 appsettings.json 的 Jwt:SecretKey）");
    return 1;
}

app.Run();
return 0;
