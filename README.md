# DIY Ambient light EVO

SimHub ambient lighting for a three screen sim racing cockpit, by [REALISTIC SIMCOCKPIT](https://www.youtube.com/@realisticsimcockpit).

The plugin drives a 60 address WS2812 Adalight controller at 115200 baud. It supports three or five parallel 60 LED strips, with a configurable fixed color, white, animations, or screen capture. Idle and in game modes switch automatically when SimHub detects a running game.

## Features

- Independent LED placement for three monitors, including group selection and movement.
- Colorloop, Rainbow, Fire Flicker, and Loading animations.
- Game profiles that load automatically when their name matches the game detected by SimHub.
- Telemetry alerts for flags, spotter, ABS, traction control, wheel lock, and pit entry. The pit alert flashes the odd numbered LEDs in blue.
- RPM lighting with a red flash at 100%.
- Arduino Nano firmware update from the plugin. The supplied firmware starts dark and turns the LEDs off after one second without a complete frame.

## Install

1. Download `DIYAmbient.Plugin.dll` from the [latest release](https://github.com/realisticsimcockpit/DIY-Ambient/releases/latest).
2. Close SimHub and copy the DLL into the SimHub installation folder, beside `SimHubWPF.exe`.
3. Restart SimHub and open **DIY Ambient light EVO**. Set the COM port and check the monitor to LED mapping in **Installation**.

The interface is in French. The current firmware uses pin D6, 60 LED addresses, RGB order, and 115200 baud. The tested board is an Arduino Nano ATmega328P with the standard bootloader. The firmware tab prepares Arduino CLI and FastLED, downloads the sketch from this repository, and uploads it only after an explicit confirmation.

The plugin uses a software current model with a 15 A limit for the configured three or five parallel strips. This is an estimate, not an electrical measurement or a substitute for suitable wiring and power distribution.

## Build

On Windows, with SimHub and .NET Framework 4.8 installed, run `CONSTRUIRE.cmd`. The script runs the C# tests and builds `artifacts/plugin/DIYAmbient.Plugin.dll` against the SimHub assemblies on that computer. To run the C# tests alone, use `TESTER.cmd`.

The plugin is a single in process DLL. Arduino CLI is downloaded only when firmware maintenance is requested; it is not part of normal lighting operation. The firmware source is in `firmware/Adalight_WS2812/`.

The firmware adapts [Wifsimster's Adalight WS2812 sketch](https://github.com/Wifsimster/adalight_ws2812); its original author credit is retained in the source.

## Notes

Screen capture currently uses Windows GDI in SDR. HDR and exclusive fullscreen capture are not implemented. Telemetry availability depends on the game data exposed by SimHub. A manual alert test checks the LED pattern but cannot prove that a particular game reports that signal.

Settings and profiles are stored under `%LOCALAPPDATA%\DIY-Ambient`. The plugin sends black on normal shutdown unless the keep last color option is enabled. The updated firmware also turns the LEDs off if complete frames stop arriving.
