# 简体中文界面（zh-CN）

本版本提供 ExileAPI 菜单、设置、插件名称、提示及常用插件界面文字的简体中文翻译。

## 使用

1. 在 Windows 上安装原项目要求的 .NET 10 等运行环境。
2. 双击根目录的 `Launch-zh-CN.cmd`。直接启动 `Loader.exe` 也默认使用中文。
3. 按 F12 打开菜单。默认的 `unifont` 字体支持中文；若已有配置使用其他字体，请在“字体设置”中选择 `unifont:16` 并应用。
4. 需要英文界面时，退出程序后运行 `Launch-English.cmd`。也可将 `config/language.txt` 改为 `en-US`，作为直接启动时的默认语言。

语言切换需要重新启动。启动器通过仅对本次启动生效的 `EXILEAPI_LANGUAGE` 环境变量覆盖默认语言，不修改已有配置。

翻译在显示文字时应用，不会修改游戏内存标识、插件配置键、输入框内容或筛选表达式。游戏数据中的物品、技能与词缀说明不属于完整的国服数据适配。未收录的文字保留原文。

## Implementation and maintenance

- `config/localization/zh-CN.json` is the UTF-8 English-to-Chinese catalog. It includes settings-name aliases and explicitly authored `{0}`–`{4}` templates for changing status messages. Edit translations here and restart; rebuilding is unnecessary for catalog edits.
- `ImGui.NET.dll` contains the localization bridge and display hooks. `ExileCore.dll`, `Loader.dll`, and all plugin DLLs retain their original bytes. No extra localization runtime DLL is needed.
- The source for the embedded bridge is in `Runtime/ChineseLocalization.cs`; the reproducible, version-checked patcher is in `Patcher/Program.cs`.
- Both string and span overloads are covered. Text measurement and drawing use the same catalog. Ranged measurement and APIs that return a pointer into the original string are deliberately left untouched.
- Explicit ImGui `###` IDs are preserved. Other translated labels use the complete English label (including any `##` suffix) as their new stable ID suffix, keeping equal captions with different IDs distinct. Those controls may reset saved expansion/window state when first localized or when switching to English. Pure ID APIs and modal popup names remain unchanged to keep popup opening and closing functional.
- File-based font loaders request the full Chinese glyph range while Chinese is enabled. The bundled Unicode font supplies the glyphs. Custom fonts still need Chinese character coverage.
- Unknown languages, missing files, and malformed JSON fall back to English. Missing catalog entries retain their original text.

The patcher accepts only the original UI library with SHA-256
`439ddeb2609edd6402af5fb0dadcbfce3b22af9fb3c206eba56ab99c70193ff6`.
It recognizes an already patched library and leaves it alone. Future upstream UI library versions must be reviewed before updating that fingerprint. Reapplying source changes requires a clean copy of the matching upstream `ImGui.NET.dll` first; retain the current localized file until the replacement passes verification.

From the repository root, with a .NET SDK supporting `net8.0` installed:

```sh
dotnet run --project Localization/Patcher -c Release -- .
dotnet run --project Localization/Tests -- .
python3 Localization/verify_catalog.py
```

The patcher uses Mono.Cecil 0.11.6. For an offline build, pass `-p:CecilPath=/absolute/path/to/Mono.Cecil.dll` to `dotnet build` instead of restoring that package.

On macOS or Linux, the optional test-only native shim verifies calls through the patched ImGui wrappers without running the Windows overlay:

```sh
cc -shared -fPIC Localization/Tests/native.c -o /tmp/exileapi-cimgui-test.so
dotnet run --project Localization/Tests -- . /tmp/exileapi-cimgui-test.so
```

Never put the test shim in the application directory. It is not a replacement for the real `cimgui.dll`.

The automated checks cover translation, English fallback, unchanged editable input and source arrays, known game identifiers, explicit control IDs, distinct duplicate captions, embedded assembly loading, font-range selection, and malformed/missing catalogs. Full visual layout, interaction with live plugins, and game integration still require testing on Windows.
