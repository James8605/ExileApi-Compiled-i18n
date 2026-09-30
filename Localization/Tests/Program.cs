using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

string root = Path.GetFullPath(args[0]);
int checks = 0;
void Equal<T>(T expected, T actual)
{
    checks++;
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected [{expected}], got [{actual}]");
}
var catalog = JsonSerializer.Deserialize<Dictionary<string, string>>(
    File.ReadAllText(Path.Combine(root, "config/localization/zh-CN.json")))!;
Equal(true, catalog.Count > 1000);
Environment.SetEnvironmentVariable("EXILEAPI_LANGUAGE", "zh-CN");
var context = new AssemblyLoadContext("Chinese", isCollectible: true);
var assembly = context.LoadFromAssemblyPath(Path.Combine(root, "ImGui.NET.dll"));
var bridge = assembly.GetType("ExileApi.Localization.ChineseLocalization", throwOnError: true)!;
var text = bridge.GetMethod("Text")!.CreateDelegate<Func<string?, string?>>();
var label = bridge.GetMethod("Label")!.CreateDelegate<Func<string?, string?>>();
var spanText = bridge.GetMethod("TextSpan")!.CreateDelegate<SpanTranslator>();
var spanLabel = bridge.GetMethod("LabelSpan")!.CreateDelegate<SpanTranslator>();
Equal("设置", text("Settings"));
Equal("设置", spanText("Settings").ToString());
Equal(null, text(null));
Equal("", text(""));
Equal("  设置\n", text("  Settings\n"));
Equal("设置： ", text("Settings: "));
Equal("设置###Settings", label("Settings"));
Equal("设置###Settings##plugin42", label("Settings##plugin42"));
Equal("设置###fixed", label("Settings###fixed"));
Equal("##hidden", label("##hidden"));
Equal("设置###Settings", label(label("Settings")));
Equal(label("Settings##a"), spanLabel("Settings##a").ToString());
Equal("SomeUnknownPlugin", text("SomeUnknownPlugin"));
Equal("Metadata/Items/Flasks/LifeFlask1", text("Metadata/Items/Flasks/LifeFlask1"));
Equal("ClassName=StackableCurrency,BaseName!^Remnant", text("ClassName=StackableCurrency,BaseName!^Remnant"));
Equal("{percent}% {current}/{total}", text("{percent}% {current}/{total}"));
Equal("已可用的混沌石套装：3", text("Chaos sets ready: 3"));
Equal("My {1} Plugin 的操作", text("Actions for My {1} Plugin"));
Equal("设置\0名称\0\0", bridge.GetMethod("ZeroSeparatedItems")!.Invoke(null, new object[] { "Settings\0Name\0\0" }));
string[] items = ["Settings", "Name", "Unrecognized"];
var translatedItems = (string[])bridge.GetMethod("Items")!.Invoke(null, new object[] { items })!;
Equal("Settings", items[0]);
Equal("设置", translatedItems[0]);
Equal("Unrecognized", translatedItems[2]);

// Explicit ### IDs survive translation. New suffixes distinguish same-caption controls.
uint Hash(string input, uint seed)
{
    uint crc = ~seed;
    byte[] bytes = Encoding.UTF8.GetBytes(input);
    for (int i = 0; i < bytes.Length; i++)
    {
        if (i + 2 < bytes.Length && bytes[i] == '#' && bytes[i + 1] == '#' && bytes[i + 2] == '#') crc = ~seed;
        crc ^= bytes[i];
        for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xEDB88320u : 0);
    }
    return ~crc;
}
foreach (uint seed in new uint[] { 0, 1234567 })
foreach (string original in new[] { "Settings###fixed", "##hidden" })
    Equal(Hash(original, seed), Hash(label(original)!, seed));
Equal(Hash("Settings###Settings", 0), Hash(label("Settings")!, 0));
Equal(false, Hash(label("Settings##a")!, 0) == Hash(label("Settings##b")!, 0));

// Optional test-only native shim exercises the actual patched wrappers on macOS/Linux.
if (args.Length > 1)
{
    nint native = NativeLibrary.Load(Path.GetFullPath(args[1]));
    NativeLibrary.SetDllImportResolver(assembly, (name, _, _) => name == "cimgui" ? native : 0);
    var lastText = Marshal.GetDelegateForFunctionPointer<GetPointer>(NativeLibrary.GetExport(native, "TestLastText"));
    var lastInput = Marshal.GetDelegateForFunctionPointer<GetPointer>(NativeLibrary.GetExport(native, "TestLastInput"));
    var lastGlyphs = Marshal.GetDelegateForFunctionPointer<GetPointer>(NativeLibrary.GetExport(native, "TestLastGlyphs"));
    var chineseGlyphs = Marshal.GetDelegateForFunctionPointer<GetPointer>(NativeLibrary.GetExport(native, "TestChineseGlyphs"));
    var imgui = assembly.GetType("ImGuiNET.ImGui")!;
    imgui.GetMethod("TextUnformatted", new[] { typeof(string) })!.Invoke(null, new object[] { "Settings" });
    Equal("设置", Marshal.PtrToStringUTF8(lastText()));
    imgui.GetMethod("Button", new[] { typeof(string) })!.Invoke(null, new object[] { "Settings##a" });
    Equal("设置###Settings##a", Marshal.PtrToStringUTF8(lastText()));
    imgui.GetMethod("Button", new[] { typeof(ReadOnlySpan<char>) })!.CreateDelegate<SpanButton>()("Settings##span");
    Equal("设置###Settings##span", Marshal.PtrToStringUTF8(lastText()));
    imgui.GetMethod("CalcTextSize", new[] { typeof(string) })!.Invoke(null, new object[] { "Settings" });
    Equal("设置", Marshal.PtrToStringUTF8(lastText()));
    var drawType = assembly.GetType("ImGuiNET.ImDrawListPtr")!;
    object draw = Activator.CreateInstance(drawType, new object[] { (nint)0 })!;
    drawType.GetMethod("AddText", new[] { typeof(System.Numerics.Vector2), typeof(uint), typeof(string) })!
        .Invoke(draw, new object[] { new System.Numerics.Vector2(), (uint)0, "Settings" });
    Equal("设置", Marshal.PtrToStringUTF8(lastText()));
    object[] inputArguments = ["Name", "Settings", (uint)128];
    imgui.GetMethod("InputText", new[] { typeof(string), typeof(string).MakeByRefType(), typeof(uint) })!
        .Invoke(null, inputArguments);
    Equal("名称###Name", Marshal.PtrToStringUTF8(lastText()));
    Equal("Settings", Marshal.PtrToStringUTF8(lastInput()));
    Equal("Settings", inputArguments[1]);
    var atlasType = assembly.GetType("ImGuiNET.ImFontAtlasPtr")!;
    object atlas = Activator.CreateInstance(atlasType, new object[] { (nint)0 })!;
    var configType = assembly.GetType("ImGuiNET.ImFontConfigPtr")!;
    atlasType.GetMethod("AddFontFromFileTTF", new[] { typeof(string), typeof(float), configType, typeof(nint) })!
        .Invoke(atlas, new[] { (object)"fonts/unifont.otf", 16f, Activator.CreateInstance(configType)!, (nint)123 });
    Equal(chineseGlyphs(), lastGlyphs());
    atlasType.GetMethod("AddFontFromFileTTF", new[] { typeof(string), typeof(float) })!
        .Invoke(atlas, new object[] { "fonts/unifont.otf", 16f });
    Equal(chineseGlyphs(), lastGlyphs());
}

Environment.SetEnvironmentVariable("EXILEAPI_LANGUAGE", "en-US");
var english = new AssemblyLoadContext("English", isCollectible: true)
    .LoadFromAssemblyPath(Path.Combine(root, "ImGui.NET.dll"));
var englishBridge = english.GetType(bridge.FullName!)!;
Equal("Settings", englishBridge.GetMethod("Label")!.Invoke(null, new object[] { "Settings" }));
Equal(false, englishBridge.GetMethod("IsEnabled")!.Invoke(null, null));

// Missing and malformed catalogs both fall back to English without throwing.
foreach (string? badCatalog in new string?[] { null, "{invalid json", "{\"Settings\":null}" })
{
    string temporary = Path.Combine(Path.GetTempPath(), "exile-localization-test-" + Guid.NewGuid());
    try
    {
        Directory.CreateDirectory(Path.Combine(temporary, "config/localization"));
        File.Copy(Path.Combine(root, "ImGui.NET.dll"), Path.Combine(temporary, "ImGui.NET.dll"));
        File.WriteAllText(Path.Combine(temporary, "config/language.txt"), "zh-CN");
        if (badCatalog != null) File.WriteAllText(Path.Combine(temporary, "config/localization/zh-CN.json"), badCatalog);
        Environment.SetEnvironmentVariable("EXILEAPI_LANGUAGE", null);
        var fallbackContext = new AssemblyLoadContext(temporary, isCollectible: true);
        var fallback = fallbackContext.LoadFromAssemblyPath(Path.Combine(temporary, "ImGui.NET.dll")).GetType(bridge.FullName!)!;
        Equal("Settings", fallback.GetMethod("Text")!.Invoke(null, new object[] { "Settings" }));
        fallbackContext.Unload();
    }
    finally { Directory.Delete(temporary, recursive: true); }
}
Console.WriteLine($"Passed {checks} localization checks; catalog has {catalog.Count} entries.");

public delegate ReadOnlySpan<char> SpanTranslator(ReadOnlySpan<char> value);
public delegate bool SpanButton(ReadOnlySpan<char> value);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate nint GetPointer();
