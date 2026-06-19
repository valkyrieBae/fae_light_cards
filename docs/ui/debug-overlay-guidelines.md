# Fae Light Cards UI Debug and Overlay Guidelines

This plugin draws its card-game UI with Dalamud `WindowSystem` windows, `Dalamud.Bindings.ImGui`, ImGui draw lists, shared textures from `ITextureProvider`, and per-frame `Draw()` calls. The deck, prompt, hand, and animations are graphical overlays, not ordinary form windows, so their layout rules need to be explicit.

## Current Debug-Bounds Problem

The inconsistent pink lines are caused by drawing debug rectangles directly on each ImGui window's outer edge with `ImGui.GetWindowDrawList().AddRect(windowPos, windowPos + windowSize, ...)`.

That is fragile for three reasons:

1. ImGui draw-list strokes are centered on the submitted coordinates. If the rectangle lies exactly on a window edge, half the stroke is outside the window.
2. A window draw list is clipped by the active window clip rect. Anything outside that clip rect, including half of a border stroke, can be discarded.
3. This plugin currently mixes sizing approaches. Dalamud applies `Window.Size`, `Position`, `SizeConstraints`, and conditions before `ImGui.Begin()`, but some windows also call `ImGui.SetWindowSize()` and push `WindowPadding` inside `Draw()`, after the window has already begun. That can leave the drawn rectangle, visible contents, clip rect, and latest requested size slightly out of sync for one or more frames.

That explains the observed pattern: deck only showing the top edge, prompt missing the left edge, and hand showing mostly top/bottom. The windows are not all sharing the same style state, content layout, clip rect, or timing when the debug rectangle is drawn.

## Technologies in Use

- **Dalamud `WindowSystem` / `Window`**: owns the `ImGui.Begin()` / `End()` lifecycle, calls `PreDraw()` before `Begin()`, applies window conditionals, then calls `Draw()`.
- **`Dalamud.Bindings.ImGui`**: the Dalamud-compatible ImGui binding namespace. Do not switch back to `ImGuiNET`.
- **Dear ImGui window APIs**: `SetNextWindowPos`, `SetNextWindowSize`, size constraints, style vars, window flags, cursor APIs, invisible buttons, and draw lists.
- **ImGui draw lists**: `GetWindowDrawList()` for drawing clipped to a window, `GetForegroundDrawList()` for screen-space overlay primitives, and draw-list clip rect APIs when a custom clipping region is needed.
- **Dalamud texture APIs**: `ITextureProvider.GetFromFile()` returns shared textures; resolve wraps per frame with `GetWrapOrEmpty()` and never cache `IDalamudTextureWrap` across frames.
- **Dalamud font atlas handles**: pushed around text drawing with `IFontHandle.Push()`.

## Layout Rules

Use one source of truth per window:

- For default overlay placement, compute the default `Position` in `PreDraw()` from `ImGui.GetMainViewport().Pos` and `.Size`.
- Preserve custom dragged positions. Auto-centering should only run while the element is in its default/unmoved state, or after an explicit reset.
- For fixed-size overlay windows, set `Window.Size` and `SizeCondition` before `Begin()` through the `Window` properties, not only with `ImGui.SetWindowSize()` inside `Draw()`.
- Treat `ImGui.SetWindowSize()` inside `Draw()` as a last resort. Dear ImGui documents current-window `SetWindowSize()` as not recommended because it can incur tearing and side effects; prefer next-window sizing, which in Dalamud means `Window.Size`, `SizeCondition`, and `SizeConstraints`.
- Do not push `ImGuiStyleVar.WindowPadding` inside `Draw()` expecting it to define the current window's content or clip rect. If window padding must affect the window begin/layout calculation, push it before `Begin()` in `PreDraw()` and pop it in `PostDraw()`, or avoid depending on padding and use explicit screen-space positions.
- Keep model dimensions, window dimensions, content start positions, invisible hit targets, and debug bounds derived from the same local variables in the same frame.

## Draw-List Rules

Choose the draw list based on intent:

- Use `GetWindowDrawList()` for normal content that should be clipped to the window.
- Use `GetForegroundDrawList()` for diagnostic overlays, animation cards moving between windows, or bounds that must remain visible even when they coincide with a window edge.
- If debug bounds should show the window's exact screen-space rectangle, draw them on the foreground draw list.
- If debug bounds should show the clipped content/canvas rectangle, inset them by at least half the stroke thickness before drawing on the window draw list.
- Never draw a stroked rectangle exactly on the clip boundary unless clipping is intentional.

Preferred debug-bounds helper shape:

```csharp
private static void DrawDebugRect(Vector2 min, Vector2 max, uint color, float thickness = 2f)
{
    var inset = MathF.Ceiling(thickness * 0.5f);
    ImGui.GetForegroundDrawList().AddRect(
        min + new Vector2(inset),
        max - new Vector2(inset),
        color,
        0f,
        ImDrawFlags.None,
        thickness);
}
```

For content-local bounds that should respect the window clip:

```csharp
private static void DrawClippedDebugRect(Vector2 min, Vector2 max, uint color, float thickness = 2f)
{
    var inset = MathF.Ceiling(thickness * 0.5f);
    ImGui.GetWindowDrawList().AddRect(
        min + new Vector2(inset),
        max - new Vector2(inset),
        color,
        0f,
        ImDrawFlags.None,
        thickness);
}
```

## Dotted and Dashed Lines

The hand placeholder uses dashed line segments, not true dots. That is fine visually, but it needs the same coordinate discipline:

- Draw all four edges with the same draw list.
- Scale dash length, gap length, and thickness with `HandScale`.
- Inset by half the stroke thickness so edge strokes are not clipped.
- If the outline is a debug/placement visual rather than content, prefer the foreground draw list.
- If it is content, ensure the hand window is larger than the card rectangle by at least the stroke thickness plus padding.

## Window-Specific Guidance

### Deck

- The deck is a texture-backed overlay.
- Window size should equal the visible deck texture size at the current scale.
- Debug bounds should be foreground/inset if they represent the visible deck bounds.
- Window padding must be zero before the window begins if the texture is expected to fill the window exactly.

### Prompt

- Prompt size should be fixed and explicit.
- Text, buttons, debug bounds, and drag hit area should all use the same `PromptSize`.
- Do not use `ImGui.GetWindowSize()` in the same frame after forcing a size inside `Draw()` and then treat it as authoritative. Prefer the constant/window property that was used to size the window.

### Hand

- Hand window size should be computed from card dimensions, spacing, and padding.
- Card content start should be computed from the same window width used to position the window.
- When the hand is in default layout, card count changes should keep the hand centered on the viewport.
- When the hand has been manually dragged, card count changes may preserve the custom center by shifting X by half the width delta.
- Dashed placeholder drawing should not depend on the window edge clip rect.

## Verification Checklist

Before calling a UI issue fixed:

- Turn on debug bounds.
- Check deck, prompt, and hand in empty-hand state.
- Add cards one by one and verify default horizontal centering remains stable.
- Drag each element, add cards, and verify the custom placement is preserved.
- Toggle lock/unlock and confirm drag hit areas still match visible bounds.
- Test at several `DeckScale` and `HandScale` values.
- Confirm pink bounds show all four sides consistently.
- Confirm dashed placeholder shows all four sides consistently.
- Run `dotnet build FaeLightCards.csproj`.

## Research Notes

- Dalamud `WindowHost` calls `Window.PreDraw()`, applies conditional window state, then begins the ImGui window and calls `Window.Draw()`. This means window size/position/style decisions that affect `Begin()` must be set before or during `PreDraw()`.
- Dalamud scales `Window.Size` and `Window.SizeConstraints` through `ImGuiHelpers.GlobalScale` before applying them as next-window size state.
- Dear ImGui documents `SetNextWindowSize()` as the preferred pre-`Begin()` sizing path and marks current-window `SetWindowSize()` as not recommended because of tearing and side effects.
- Dear ImGui's draw-list clip rects are render scissor state. `ImGui.PushClipRect()` affects logic/hit testing; `ImDrawList.PushClipRect()` is render-only.
- AetherPool's custom rendering is a useful local pattern: draw regular table content through the window draw list, but draw overlay/floating messages through the foreground draw list.

## Source References

- Local Dalamud window lifecycle: `references/Dalamud/Dalamud/Interface/Windowing/WindowHost.cs`
- Local Dalamud window properties: `references/Dalamud/Dalamud/Interface/Windowing/Window.cs`
- Local Dalamud texture lifecycle: `references/Dalamud/Dalamud/Plugin/Services/ITextureProvider.cs`
- Local AetherPool draw-list examples: `references/AetherPool/Windows/UIManager.cs`
- Dear ImGui API notes: https://raw.githubusercontent.com/ocornut/imgui/master/imgui.h
