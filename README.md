# WoW Twitch Overlay

A World of Warcraft-themed Windows overlay that allows you to watch Twitch streams seamlessly over WoW without the stream disappearing when you play or click in-game.

Styled with an authentic WoW backpack interface frame, golden metallic borders, Twitch medallion portrait, and custom controls.

---

## ⚡ Quick Start

1. **Set WoW Display Mode to Windowed (Fullscreen):**
   - In WoW: **Game Menu (Esc) → Options → Graphics → Display Mode → Windowed (Fullscreen)**.
2. **Launch the Overlay:**
   - Double-click [`Start-Overlay.bat`](Start-Overlay.bat) (or run `dist/TwitchWoWOverlay.exe`).
3. **Select Streamer:**
   - Type any channel name (e.g. `asmongold`, `shroud`) or paste a Twitch URL and hit **Enter** or click **Load**.
4. **Position & Resize:**
   - Drag anywhere on the header bar to move.
   - Drag the bottom-right corner grip to resize.
5. **Adjust Opacity & Volume:**
   - Use the **Opacity** slider to make the stream semi-translucent over WoW.
   - Use the **Volume** slider or **Mute** button to control stream volume.
6. **Lock Click-Through Mode:**
   - Press **`Ctrl + Shift + T`** (or **`Ctrl + F9`**):
     - The controls bar collapses, leaving the authentic WoW frame around the stream.
     - **All mouse clicks, camera drags, and abilities pass straight through into WoW!**
   - Press **`Ctrl + Shift + T`** again whenever you want to move, resize, change channels, or adjust volume.

---

## 🎮 Features

- **Never Disappears:** Built with Windows `WS_EX_TOPMOST` so it stays pinned above WoW even when clicking in-game.
- **Click-Through Mode (`Ctrl + Shift + T` / `Ctrl + F9`):** Pass all clicks straight to WoW so your gameplay is never interrupted.
- **WoW Interface Frame:** Golden beveled borders, Twitch portrait medallion, WoW gold title font, and red close button.
- **Dynamic Opacity (10% - 100%):** Hardware-accelerated transparency so you can see game mechanics through the stream.
- **Integrated Volume & Mute Controls:** Dedicated volume slider and mute toggle right in the WoW toolbar.
- **Display Modes:** Video Only (`popout`), Video + Chat, or Chat Only.
- **Persistent Settings:** Automatically remembers your last streamer, window position, size, opacity, and volume.

---

## 🛠️ Building & Developing

Requires .NET 8 SDK:

```powershell
# Build
dotnet build

# Publish locally
dotnet publish -c Release -r win-x64 --self-contained false -o ./dist
```
