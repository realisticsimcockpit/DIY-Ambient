# DIY-Ambient development constraints

- Development is PRIVATE. Verify repository visibility before any push. Never create a public fork, paste, release, or public CI job containing this source.
- User owns realisticsimcockpit/DIY-Ambient. Do not publish to other accounts/repositories.
- One in-process SimHub DLL. No external runtime application without a demonstrated need and a user-approved architecture change.
- Fixed 60 LEDs / Adalight / 115200 baud / RGB for the supplied firmware. Never shorten physical output to the telemetry count. Never flash the board automatically.
- One telemetry setting N, even 0..60, exactly N/2 LEDs on each side. Preserve background when an effect is inactive.
- Three independent monitors, no Surround. Preserve default ranges DISPLAY1 center 21..40, DISPLAY2 1..20, DISPLAY3 41..60. IDs and physical geometry must be confirmed in setup.
- Simple daily UI. Calibration and power setup remain separate. Do not reintroduce a large effects/settings editor.
- Default OFF and first-run preview-only. Every output, including identification and alert tests, uses the common estimated power cap. Do not claim measured/guaranteed electrical safety.
- Windows/SimHub tests are mandatory. GDI is currently a prototype backend, not completed DXGI/HDR support. Unsupported telemetry remains inactive and is reported.
- Use installed real SimHub SDK DLLs, never distribute host binaries or make production stubs. Keep .NET Framework 4.8 / C# 5 compatibility unless the build strategy is explicitly updated.
- Run the provided C# tests on Windows. Python source checks do not substitute for compiling or executing C#.

- White balance is not a global filter: apply it only to fixed-white background/white identification, never captured colours or telemetry. Save canonical settings, not temporary previews.
- Keep output gain stable for a configured power budget. Dynamic per-frame gain must not make unaffected LEDs brighten during alerts. Every mode remains subject to the same conservative model.
- Invalidate captures by layout generation and age. Serial configuration/permission changes disarm atomically. Native/serial completion may still need bounded shutdown; do not claim process isolation or hardware acknowledgements.
- Audit 0.1.1: 17 source checks + 7 independent Python models + 194 C++ parser simulations executed. 53 C# tests PROVIDED, not executed in the audit environment. Keep these categories distinct.
