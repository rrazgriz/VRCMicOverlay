# VRCMicOverlay

OpenVR Overlay for a custom microphone icon and sounds for VRChat. Uses OSCQuery to eliminate conflicts with other OSC listeners.

Features:

- Icon customization
- Customize icon fadeout time/amount
- Use your own mute/unmute sfx

This was made to learn more about SteamVR/OpenVR overlays, and because I wanted more control than the VRChat settings give.

## Usage

Extract to your preferred folder. All files are necessary. 

Run `VRCMicOverlay.exe` with SteamVR open. The program will register to auto-launch with SteamVR wherever it was launched from last.

It's recommended to set your default VRChat Microphone icon opacity to 0 both while muted and unmuted, instead of hiding the entire HUD. This allows other HUD elements to still be shown.

Optionally: edit the generated `settings.json` to your liking, and restart. For documentation on what each setting does, see the comments in [Configuration.cs](VRCMicOverlay/Configuration.cs).

## Known Issues

- Program will fail to init if given invalid or nonexistent files for images/sounds.

If you find other issues, feel free to file a GitHub issue.

## Attribution 

- Microphone icon by Dave Gandy (CC-BY): https://www.flaticon.com/free-icon/microphone-black-shape_25682
- Av3Emulator (SimpleOSC), by Lyuma (MIT): https://github.com/lyuma/Av3Emulator/
- Mute/unmute sound by a deleted user on Freesound (CC0): https://freesound.org/people/deleted_user_7146007/sounds/383659/
- Others I've forgotten/included via comments