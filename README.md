# VRCMicOverlay

OpenVR Overlay for a custom microphone icon and sounds for VRChat. This was made so I could learn more about SteamVR/OpenVR overlays, and because I wanted more control than the VRChat settings give.

Features include icon and sound asset customization, configurable fadeout timers and faded opacity, and more. It also uses OSCQuery to eliminate conflicts with other OSC listeners.

VRCMicOverlay is a console application that uses a simple program structure, with global state, a basic startup procedure, and a tight inner loop that does only what it needs to. This limits flexibility, but it was easy to get up and running quickly, performs well, and is reliable - I've used this overlay as a full replacement for VRChat's Microphone Icon for hundreds of hours with minimal issues.

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
- SimpleOSC (from Av3Emulator), by Lyuma (MIT): https://github.com/lyuma/Av3Emulator/
- Mute/unmute sound by a deleted user on Freesound (CC0): https://freesound.org/people/deleted_user_7146007/sounds/383659/
- Others I've forgotten/included via comments