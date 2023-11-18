# 🎙️ VRCMicOverlay

## [📦 Download Latest Release](https://github.com/rrazgriz/VRCMicOverlay/releases/latest)

OpenVR Overlay written in C# that implements a custom microphone icon and sounds for VRChat. This was made so I could learn more about SteamVR/OpenVR overlays, and because I wanted more control than the VRChat implementation gives.

Features include icon and sound asset customization, configurable fadeout timers and faded opacity, and more. It also uses OSCQuery to eliminate conflicts with other OSC listeners.

Using an OpenVR overlay offers a number of advantages, most notably very, very smooth operation even under adverse application performance, as seen in this recording (top is VRCMicOverlay, bottom is the VRChat native Mic Icon):

https://github.com/rrazgriz/VRCMicOverlay/assets/47901762/16342ff6-b003-4f92-a988-b6395f17f6cd

Structurally, VRCMicOverlay is a console application that uses a simple program structure with a tight inner loop that does only what it needs to. It has no runtime input and is configured entirely through a generated `settings.json` file. This limits flexibility, but it was easy to get up and running quickly, performs well, and is reliable - I've used this overlay as a full replacement for VRChat's Microphone Icon for hundreds of hours with minimal issues.

If you find issues, feel free to file a GitHub issue. I can't guarantee I'll be able to fix things, though - this is first and foremost a personal application for my use, so if it's working for me, I'm happy!

## Usage

1. **Download** [the latest release](https://github.com/rrazgriz/VRCMicOverlay/releases/latest) and **extract** to your preferred folder. All files are necessary. 
2. **Run** `VRCMicOverlay.exe` with SteamVR open. The first time, you'll probably get a security warning since I don't have a key to sign the exe with.
3. The program will register to **auto-launch** with SteamVR wherever it was launched from last. Disable this in SteamVR if for some reason you don't want it.
4. Optionally: Edit the generated `settings.json` to your liking, and restart. For documentation on what each setting does, see the comments in [Configuration.cs](VRCMicOverlay/Configuration.cs).
5. It's recommended to set your default VRChat Microphone icon opacity to 0 both while muted and unmuted, instead of hiding the entire HUD. This allows other HUD elements to still be shown.

## Notes

- The most important settings to change to tune your experience are:
  - `ICON_MUTED_MAX_ALPHA` (and min/unmuted variants) - 0 to 1, how transparent (0) or opaque (1) the mic should be in the fully unfaded (MAX) faded (MIN) state
  - `MIC_MUTED_FADE_START`/`MIC_MUTED_FADE_PERIOD` (and unmuted variants) - how long, in seconds, to wait at full alpha (START) and how long the fade should be (PERIOD).
  - `ICON_TINT_MUTED`/`ICON_TINT_UNMUTED` - hex color code that applies a tint over the icon. If you use a custom icon that has multiple colors, set this to `#FFFFFF` for no tint.
  - `AUDIO_DEVICE_STARTS_WITH` - string of the first unique string of the name of your input device (these are listed at startup). If the input device you want to be listening to isn't the default, edit this.
  - `ICON_SIZE` - size (in meters, world-space width) of the mic icon. The default approximately matches VRChat's default.
- The Microphone Icon template is stored as a `.psd` in the source code's Assets folder. I edited it using Krita.
- Multiple microphone assets are included, including outline and dot variations.
- Settings are validated, and invalid values will be replaced with the defaults. I've chosen what I think are reasonable minimums and maximums for numeric values. You can find these in the `Validate()` function next to the Configuration.

## Attribution 

- Microphone icon by Dave Gandy (CC-BY): https://www.flaticon.com/free-icon/microphone-black-shape_25682
- SimpleOSC (from Av3Emulator), by Lyuma (MIT): https://github.com/lyuma/Av3Emulator/
- Custom mute/unmute sound by a deleted user on Freesound (CC0): https://freesound.org/people/deleted_user_7146007/sounds/383659/
- NAudio (MIT): https://github.com/naudio/NAudio
- VRChat.OSCQuery (MIT): https://github.com/vrchat-community/vrc-oscquery-lib
- Others I've forgotten/included via comments

## Building

This should be buildable with a .NET 6 SDK. `cd` to the VRCMicOverlay subfolder, `dotnet restore`, then `dotnet build`.
