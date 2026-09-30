// Test-only cimgui shim. Never copy this library into the application directory.
#include <stdint.h>
#include <string.h>
typedef struct { float x, y; } Vec2;
static char text[4096], input[4096];
static const uint16_t chinese[] = {0x20, 0xff, 0x4e00, 0x9faf, 0};
static const void *glyphs;
static void save(const char *value) { strncpy(text, value ? value : "", sizeof(text)-1); }
const char *TestLastText(void) { return text; }
const char *TestLastInput(void) { return input; }
const void *TestLastGlyphs(void) { return glyphs; }
const void *TestChineseGlyphs(void) { return chinese; }
void igTextUnformatted(const char *start, const char *end) { save(start); }
uint8_t igButton(const char *label, Vec2 size) { save(label); return 0; }
void igCalcTextSize(Vec2 *result, const char *start, const char *end, uint8_t hide, float wrap)
{ save(start); result->x = (float)strlen(start); result->y = 16; }
void ImDrawList_AddText_Vec2(void *list, Vec2 pos, uint32_t color, const char *start, const char *end)
{ save(start); }
uint8_t igInputText(const char *label, char *buffer, uint32_t size, int flags, void *callback, void *data)
{ save(label); strncpy(input, buffer, sizeof(input)-1); return 0; }
const void *ImFontAtlas_GetGlyphRangesChineseFull(void *atlas) { return chinese; }
void *ImFontAtlas_AddFontFromFileTTF(void *atlas, const char *filename, float size, void *config, const void *ranges)
{ glyphs = ranges; return 0; }
