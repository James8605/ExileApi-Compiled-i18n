using System.Security.Cryptography;
using Mono.Cecil;
using Mono.Cecil.Cil;

const string originalHash = "439DDEB2609EDD6402AF5FB0DADCBFCE3B22AF9FB3C206EBA56AB99C70193FF6";
const string bridgeName = "ExileApi.Localization.ChineseLocalization";
if (args.Length != 1) throw new ArgumentException("Usage: Patcher <ExileAPI directory>");
string root = Path.GetFullPath(args[0]);
string file = Path.Combine(root, "ImGui.NET.dll");
using var module = ModuleDefinition.ReadModule(file, new ReaderParameters { InMemory = true });
if (module.Types.Any(t => t.FullName == bridgeName))
{
    Console.WriteLine("This UI library is already localized. Catalog changes take effect after restarting.");
    return;
}
if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) != originalHash)
    throw new InvalidOperationException("Unsupported ImGui.NET.dll. Review a new upstream version before patching it.");

using var runtime = ModuleDefinition.ReadModule(typeof(ExileApi.Localization.ChineseLocalization).Assembly.Location,
    new ReaderParameters { ReadingMode = ReadingMode.Immediate });
var bridge = runtime.Types.Single(t => t.FullName == bridgeName);
// Cecil reads bodies lazily. Materialize them before assigning a different module.
_ = bridge.Fields.Count;
foreach (var method in bridge.Methods)
    if (method.HasBody) _ = method.Body.Instructions.Count;
runtime.Types.Remove(bridge);
module.Types.Add(bridge);

// Move the small, self-contained bridge into the existing assembly, importing BCL references.
bridge.BaseType = module.ImportReference(bridge.BaseType);
bridge.CustomAttributes.Clear();
foreach (var field in bridge.Fields)
{
    field.FieldType = module.ImportReference(field.FieldType);
    field.CustomAttributes.Clear();
}
foreach (var method in bridge.Methods)
{
    method.ReturnType = module.ImportReference(method.ReturnType);
    method.CustomAttributes.Clear();
    method.MethodReturnType.CustomAttributes.Clear();
    foreach (var parameter in method.Parameters)
    {
        parameter.ParameterType = module.ImportReference(parameter.ParameterType);
        parameter.CustomAttributes.Clear();
    }
    if (!method.HasBody) continue;
    foreach (var variable in method.Body.Variables)
        variable.VariableType = module.ImportReference(variable.VariableType);
    foreach (var handler in method.Body.ExceptionHandlers)
        if (handler.CatchType != null) handler.CatchType = module.ImportReference(handler.CatchType);
    foreach (var instruction in method.Body.Instructions)
        instruction.Operand = instruction.Operand switch
        {
            MethodReference m when m.DeclaringType.FullName != bridgeName => module.ImportReference(m),
            FieldReference f when f.DeclaringType.FullName != bridgeName => module.ImportReference(f),
            TypeReference t when t.FullName != bridgeName => module.ImportReference(t),
            _ => instruction.Operand
        };
}

MethodDefinition Bridge(string name) => bridge.Methods.SingleOrDefault(m => m.Name == name)
    ?? throw new InvalidOperationException($"Missing localization bridge method: {name}");
int textHooks = 0, fontHooks = 0;
foreach (var type in module.Types.Where(t => t.Namespace == "ImGuiNET"))
foreach (var method in type.Methods.Where(m => m.HasBody))
{
    var first = method.Body.Instructions[0];
    var il = method.Body.GetILProcessor();
    foreach (var parameter in method.Parameters)
    {
        string? translator = null;
        string name = parameter.Name;
        bool isString = parameter.ParameterType.FullName == "System.String";
        bool isSpan = parameter.ParameterType.FullName == "System.ReadOnlySpan`1<System.Char>";
        if (type.Name == "ImGui")
        {
            if (name == "label" || (name == "name" && method.Name == "Begin"))
                translator = "Label";
            else if (name is "fmt" or "text" or "hint" or "preview_value" or "overlay_text")
                translator = "Text";
            else if (name == "items_separated_by_zeros") translator = "ZeroSeparatedItems";
            else if (name == "items" && parameter.ParameterType.FullName == "System.String[]") translator = "Items";

            // Ranged text measurement uses offsets into the ORIGINAL buffer; leave it intact.
            if (method.Name.StartsWith("CalcTextSize") && method.Parameters.Any(p => p.Name is "start" or "length"))
                translator = null;
            // Table column headings have a separate user_id; ### is not needed in their text.
            if (method.Name == "TableSetupColumn" && name == "label") translator = "Text";
        }
        else if ((type.Name == "ImDrawListPtr" && method.Name == "AddText") ||
                 (type.Name == "ImFontPtr" && method.Name is "CalcTextSizeA" or "RenderText"))
        {
            if (name == "text_begin" && !method.Parameters.Any(p => p.Name == "remaining")) translator = "Text";
        }
        if (translator == null || (!isString && !isSpan && translator != "Items")) continue;
        if (isSpan) translator += "Span";
        il.InsertBefore(first, il.Create(OpCodes.Ldarg, parameter));
        il.InsertBefore(first, il.Create(OpCodes.Call, Bridge(translator)));
        il.InsertBefore(first, il.Create(OpCodes.Starg, parameter));
        textHooks++;
    }
    if (type.Name == "ImFontAtlasPtr" && method.Name == "AddFontFromFileTTF")
    {
        var nativeCall = method.Body.Instructions.Single(i => i.Operand is MethodReference m &&
            m.DeclaringType.Name == "ImGuiNative" && m.Name == "ImFontAtlas_AddFontFromFileTTF");
        // The last stack argument is the glyph-range pointer. Replace only that argument,
        // including overloads that pass null for default glyph ranges.
        il.InsertBefore(nativeCall, il.Create(OpCodes.Call, Bridge("IsEnabled")));
        il.InsertBefore(nativeCall, il.Create(OpCodes.Brfalse, nativeCall));
        il.InsertBefore(nativeCall, il.Create(OpCodes.Pop));
        il.InsertBefore(nativeCall, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(nativeCall, il.Create(OpCodes.Call, type.Methods.Single(m => m.Name == "GetGlyphRangesChineseFull")));
        il.InsertBefore(nativeCall, il.Create(OpCodes.Conv_U));
        fontHooks++;
    }
}
if (textHooks < 100 || fontHooks != 6) throw new InvalidOperationException("Unexpected UI surface; no output written.");
string temporary = file + ".localized";
module.Write(temporary);
File.Move(temporary, file, overwrite: true);
Console.WriteLine($"Localized {textHooks} text parameters and {fontHooks} font loaders. Core and plugin binaries are unchanged.");
