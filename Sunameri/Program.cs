﻿using System.Reflection;

using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;

ConsoleApp.Run(args, ([Option(0, "scriptfile")] string input) =>
{
    var inputPath = GetRootedPath(input);
    var __dirname = Path.GetDirectoryName(inputPath);

    var engines = new V8ScriptEngine[Environment.ProcessorCount];
    using (var engine = new V8ScriptEngine())
    {
        // ^C を入力するとスクリプトを停止する
        Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs e) =>
        {
            Console.WriteLine("Execution cancelled.");
            engine.Interrupt();
            Environment.Exit(1);
        };
        
        // System 以下のものを何でも使える
        engine.AddHostObject("dotnet", HostItemFlags.GlobalMembers, new HostTypeCollection("mscorlib", "System.Core"));

        // seed = newVar(System.UInt32); など必須
        engine.AddHostObject("host", HostItemFlags.GlobalMembers, new HostFunctions());

        // OpenCvSharp
        engine.AddHostTypes(Assembly.Load(nameof(OpenCvSharp)).GetTypes());

        engine.AddHostTypes(new Type[]
        {
            typeof(JavaScriptExtensions), // 要らないかも
            typeof(SerialPortWrapper),
            typeof(KeyDown),
            typeof(KeyUp),
            typeof(xAxis),
            typeof(yAxis),
            typeof(cxAxis),
            typeof(cyAxis),
            typeof(SerialPortWrapper),
            typeof(VideoCaptureWrapper),
            typeof(MatExtension),
            typeof(Timer)
        });

        // Node.js風にスクリプトがあるフォルダを表す
        engine.Execute(string.Format("const __filename = \"{0}\"; const __dirname = \"{1}\"; ", inputPath.Replace("\\","\\\\"), __dirname?.Replace("\\","\\\\")));

        // librariesディレクトリ内の*.dllから追加する
        var libraries = Path.Join(AppContext.BaseDirectory, "libraries");
        if (Directory.Exists(libraries))
            foreach (string library in Directory.GetFiles(libraries).Where(path => Path.GetExtension(path) == ".dll"))
                try
                {
                    var asm = Assembly.LoadFrom(library);
                    engine.AddHostTypes(asm.GetTypes());
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.Message);
                }

        // enable module
        engine.DocumentSettings.AccessFlags = DocumentAccessFlags.EnableFileLoading;
        engine.DocumentSettings.SearchPath = __dirname;
        
        // execute
        try
        {
            engine.Execute(new DocumentInfo { Category = ModuleCategory.Standard }, File.ReadAllText(inputPath));
        }
        catch (ScriptInterruptedException)
        {
            // 本当はここで止まってほしいが、だめみたい
            // Environment.Exit(1); で吹き飛ばしている。
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Environment.Exit(1);
        }
    }
});

/// <summary>
/// 絶対パスに変換する
/// </summary>
/// <param name="path">パス文字列</param>
/// <returns></returns>
string GetRootedPath(string path)
{
    return Path.IsPathRooted(path) ? path : Path.Join(AppContext.BaseDirectory, path);
}
