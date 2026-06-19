# Dalamud Plugin Best Practices & Guidelines

This document outlines the coding standards, resource lifecycle constraints, and best practices for developing Dalamud plugins in FFXIV, gathered from Dalamud's official documentation and codebase reviews.

---

## 1. Plugin Lifecycle & Resource Disposal
Since plugins run directly inside the FFXIV game process, memory leaks or unhandled exceptions can crash the entire game. Proper cleanup is critical.

- **Mandatory `Dispose` Implementation:** Your plugin main class must implement `IDalamudPlugin` and handle cleanups in `Dispose()`.
- **Event Unsubscription:** Every `+=` subscription to a game or framework event must have a matching `-=` unsubscription in `Dispose()`. Common events to clean up:
  - `Framework.Update`
  - `ChatGui.ChatMessage`
  - `CommandManager` handlers
  - `UiBuilder.Draw`, `UiBuilder.OpenConfigUi`, and `UiBuilder.OpenMainUi`
- **WindowSystem Cleanup:** Call `WindowSystem.RemoveAllWindows()` in `Dispose()`.
- **Static References:** If you store references to Dalamud services or managers in static variables, nullify them in `Dispose()` to avoid holding memory references.

---

## 2. Dependency Injection (IoC)
Dalamud uses `Dalamud.IoC` to inject services into your plugin. 

- **Service Retrieval:** Always use constructor injection or properties marked with the `[PluginService]` attribute to access services (e.g., `ICommandManager`, `ITextureProvider`, `IPluginLog`).
- **Do Not Dispose Services:** Dalamud manages the lifecycle of these injected services. Never call `Dispose()` on services obtained via IoC.
- **Service Registration:** If you write custom services, register them inside your dependency container or instantiate them manually in the plugin constructor and dispose of them in `Dispose()`.

---

## 3. UI Development & ImGui
FFXIV plugins build their UI overlays using Dear ImGui (via `Dalamud.Bindings.ImGui` bindings).

- **Use the Window System:** Inherit from `Dalamud.Interface.Windowing.Window` and add windows to a central `WindowSystem`. This ensures proper integration with the game UI and correct input focus handling.
- **Register UI Callbacks:** You must hook into `PluginInterface.UiBuilder.Draw`, `PluginInterface.UiBuilder.OpenConfigUi`, and `PluginInterface.UiBuilder.OpenMainUi` (Main UI callback).
- **Zero Texture Cache Caching:**
  - When loading image assets, obtain an `ISharedImmediateTexture` using `ITextureProvider.GetFromFile()`.
  - Do not cache `IDalamudTextureWrap` instances across frames.
  - In your `Draw()` loop, always resolve the texture using `texture.GetWrapOrEmpty()` or `texture.TryGetWrap(out var wrap)`.
  - Do not manually dispose of wraps resolved from `ISharedImmediateTexture`.
- **Position & Size Conditions:** Use `ImGuiCond.FirstUseEver` for default sizing/positioning so players can reposition windows. Avoid `ImGuiCond.Always` unless you are explicitly resetting the position programmatically.

---

## 4. Performance & Threading
- **Keep `Draw()` Cheap:** Do not run I/O, config saves, network requests, or heavy computations inside your `Draw()` loop. Precompute positions, check simple states, and render quickly.
- **Thread Safety:** ImGui rendering occurs on the game's main UI thread. Any background networking (e.g., SignalR, WebSockets) must run on separate background tasks.
- **UI Queue Draining:** Hand off network payloads to a thread-safe queue (e.g., `ConcurrentQueue`) and drain the queue into the local game model on the main thread (e.g., inside `UiBuilder.Draw`) before drawing.
