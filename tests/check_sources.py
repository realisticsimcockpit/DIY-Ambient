"""Static/source-contract checks only. Does NOT compile or execute C#."""
from pathlib import Path
import json
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def check_delimiters(text: str, name: str) -> None:
    """Check lexical delimiter balance, excluding C# comments and string literals."""
    stack = []
    i = 0
    while i < len(text):
        if text.startswith('//', i):
            end = text.find('\n', i)
            i = len(text) if end == -1 else end + 1
            continue
        if text.startswith('/*', i):
            end = text.find('*/', i + 2)
            if end == -1:
                raise AssertionError(f'{name}: unterminated comment')
            i = end + 2
            continue
        if text.startswith('@"', i):
            i += 2
            while i < len(text):
                if text.startswith('""', i):
                    i += 2
                elif text[i] == '"':
                    i += 1
                    break
                else:
                    i += 1
            else:
                raise AssertionError(f'{name}: unterminated verbatim string')
            continue
        if text[i] in ('"', "'"):
            quote = text[i]
            i += 1
            while i < len(text):
                if text[i] == '\\':
                    i += 2
                elif text[i] == quote:
                    i += 1
                    break
                else:
                    i += 1
            else:
                raise AssertionError(f'{name}: unterminated string/character')
            continue
        char = text[i]
        if char in '({[':
            stack.append(char)
        elif char in ')}]':
            expected = {')': '(', '}': '{', ']': '['}[char]
            if not stack or stack.pop() != expected:
                raise AssertionError(f'{name}: delimiter mismatch at offset {i}')
        i += 1
    if stack:
        raise AssertionError(f'{name}: unclosed delimiters {stack}')


class SourceChecks(unittest.TestCase):
    def setUp(self):
        self.settings = json.loads((ROOT / 'docs/settings.example.json').read_text())

    def test_example_covers_exactly_60_leds(self):
        ids = []
        for display in self.settings['Displays']:
            ids.extend(range(display['FirstLed'], display['LastLed'] + 1))
        self.assertEqual(sorted(ids), list(range(1, 61)))
        self.assertEqual(sorted(z['Led'] for z in self.settings['Zones']), list(range(1, 61)))

    def test_display_mapping_matches_user_request(self):
        by_id = {d['DeviceName']: d for d in self.settings['Displays']}
        for number, lo, hi in ((1, 21, 40), (2, 1, 20), (3, 41, 60)):
            d = by_id[rf'\\.\DISPLAY{number}']
            self.assertEqual((d['FirstLed'], d['LastLed']), (lo, hi))
        self.assertEqual(by_id[r'\\.\DISPLAY1']['Position'], 0)

    def test_example_rectangles_are_valid(self):
        by_id = {d['DeviceName']: d for d in self.settings['Displays']}
        for zone in self.settings['Zones']:
            display = by_id[zone['DeviceName']]
            self.assertLessEqual(display['FirstLed'], zone['Led'])
            self.assertLessEqual(zone['Led'], display['LastLed'])
            for origin, size in (('X', 'Width'), ('Y', 'Height')):
                self.assertGreaterEqual(zone[origin], 0)
                self.assertGreater(zone[size], 0)
                self.assertLessEqual(zone[origin] + zone[size], 1.0000001)

    def test_safe_defaults_in_example_and_sources(self):
        self.assertFalse(self.settings['PreviewOnly'])
        self.assertTrue(self.settings['ElectricalConfirmed'])
        self.assertFalse(self.settings['KeepOnAfterExit'])
        self.assertTrue(self.settings['StartEnabled'])
        self.assertTrue(self.settings['StartEnabledPreferenceInitialized'])
        self.assertEqual(self.settings['LedStripCount'], 3)
        self.assertEqual(self.settings['CurrentBudgetAmps'], 15.0)
        self.assertEqual(self.settings['TelemetryLedCount'], 0)
        source = (ROOT / 'src/DIYAmbient.Core/Settings.cs').read_text()
        for fragment in ('PreviewOnly = false', 'ElectricalConfirmed = true', 'CurrentBudgetAmps = 15.0', 'KeepOnAfterExit = false', 'LedStripCount = 3', 'StartEnabled = true', 'StartEnabledPreferenceInitialized = true', 'TelemetryLedCount = 0'):
            self.assertIn(fragment, source)
        engine = (ROOT / 'src/DIYAmbient.Plugin/AmbientEngine.cs').read_text()
        self.assertIn('new EngineState(settings.Clone(), false, false, 0)', engine)

    def test_protocol_source_contract(self):
        source = (ROOT / 'src/DIYAmbient.Core/AdalightProtocol.cs').read_text()
        self.assertRegex(source, r'PacketLength\s*=\s*186')
        self.assertRegex(source, r'BaudRate\s*=\s*115200')
        self.assertIn('colors.Length != 60', source)
        self.assertIn('colors.Length - 1', source)
        self.assertIn('0x55', source)

    def test_csharp_lexical_balance(self):
        for file in sorted(ROOT.rglob('*.cs')):
            check_delimiters(file.read_text(encoding='utf-8-sig'), str(file.relative_to(ROOT)))

    def test_project_is_one_library_with_real_references(self):
        file = ROOT / 'src/DIYAmbient.Plugin/DIYAmbient.Plugin.csproj'
        project = ET.parse(file)
        ns = {'m': 'http://schemas.microsoft.com/developer/msbuild/2003'}
        self.assertEqual(project.find('.//m:OutputType', ns).text, 'Library')
        self.assertEqual(project.find('.//m:TargetFrameworkVersion', ns).text, 'v4.8')
        names = [r.attrib['Include'] for r in project.findall('.//m:Reference', ns)]
        self.assertIn('SimHub.Plugins', names)
        self.assertIn('GameReaderCommon', names)
        for inc in project.findall('.//m:Compile', ns):
            path = inc.attrib['Include'].replace('\\', '/')
            self.assertTrue(list(file.parent.glob(path)), f'Missing compile source: {path}')
        # Generated build products are not distributed source dependencies.
        for extension in ('*.dll', '*.exe'):
            unexpected = [p for p in ROOT.rglob(extension)
                if not {'artifacts', 'bin', 'obj'}.intersection(p.relative_to(ROOT).parts)]
            self.assertFalse(unexpected)

    def test_no_external_runtime_or_network_client_in_sources(self):
        # Explicit maintenance is the sole exception; the lighting runtime stays in-process.
        source = '\n'.join(f.read_text() for f in (ROOT / 'src').rglob('*.cs') if f.name != 'FirmwareFlasher.cs')
        for forbidden in ('new HttpClient(', 'new WebClient(', 'Socket(', 'TcpListener('):
            self.assertNotIn(forbidden, source)
        self.assertEqual(source.count('Process.Start('), 1)
        self.assertIn('https://www.youtube.com/@realisticsimcockpit', source)

    def test_evo_branding_is_visible_in_simhub_and_settings(self):
        plugin = (ROOT / 'src/DIYAmbient.Plugin/Plugin.cs').read_text()
        settings = (ROOT / 'src/DIYAmbient.Plugin/SettingsControl.cs').read_text()
        self.assertIn('[PluginName("DIY Ambient light EVO")]', plugin)
        self.assertIn('DIY Ambient light EVO by REALISTIC SIMCOCKPIT', settings)

    def test_csharp_scenarios_provided_but_not_run_here(self):
        source = (ROOT / 'tests/CoreTests.cs').read_text()
        self.assertEqual(len(re.findall(r'\bTest\("', source)), 79)

    def test_white_balance_is_not_a_global_screen_filter(self):
        source = (ROOT / 'src/DIYAmbient.Core/FrameComposer.cs').read_text()
        output = source.split('public static FrameResult ApplyOutputLimits', 1)[1].split('public static double Estimate', 1)[0]
        self.assertNotIn('s.Warmth', output)
        self.assertNotIn('s.Tint', output)
        self.assertIn('Settings.LedCount * s.LedStripCount * 3 * ChannelAmps', output)
        self.assertNotIn('Estimate(input)', output)

    def test_capture_uses_per_display_native_context_and_flush(self):
        source = (ROOT / 'src/DIYAmbient.Plugin/ScreenCapture.cs').read_text()
        for fragment in ('CreateDC("DISPLAY", monitor.DeviceName', 'CreateCompatibleDC(source)', 'CreateDIBSection', 'GdiFlush()', 'EnumDisplayMonitors'):
            self.assertIn(fragment, source)
        self.assertNotIn('Graphics.FromImage', source)
        self.assertNotIn('Screen.AllScreens.Select', source)

    def test_capture_results_have_layout_generation(self):
        source = (ROOT / 'src/DIYAmbient.Plugin/AmbientEngine.cs').read_text()
        self.assertIn('OutputPolicy.CanUseCapture(current.CaptureVersion, image.Version', source)
        self.assertIn('state.CaptureVersion == current.CaptureVersion', source)
        self.assertIn('OutputPolicy.HardwareChanged', source)
        self.assertIn('Monitor.TryEnter(OutputOwner', source)
        self.assertNotIn('Thread.Abort(', source)

    def test_white_controls_are_inline_and_saved_normally(self):
        source = (ROOT / 'src/DIYAmbient.Plugin/SettingsControl.cs').read_text()
        self.assertIn('whiteControls', source)
        self.assertIn('s.Warmth = warmth.Value; s.Tint = tint.Value;', source)
        self.assertNotIn('Ajuster mon blanc', source)

    def test_build_cleans_stale_outputs_and_uses_staging(self):
        source = (ROOT / 'scripts/Build.ps1').read_text(encoding='utf-8-sig')
        self.assertLess(source.index('Remove-Item -LiteralPath'), source.index("'Test.ps1'"))
        self.assertIn('stagedDll', source)
        self.assertIn('GetAssemblyName($stagedDll)', source)
        self.assertIn('build-manifest.json', source)
        self.assertIn('if ($Install)', source)

    def test_scripts_are_windows_compatible_encoded(self):
        for path in (ROOT / 'scripts').glob('*.ps1'):
            self.assertTrue(path.read_bytes().startswith(b'\xef\xbb\xbf'), path.name)
        for path in ROOT.glob('*.cmd'):
            data = path.read_bytes()
            self.assertNotIn(b'\n', data.replace(b'\r\n', b''), path.name)

    def test_unknown_enums_not_guessed_as_telemetry(self):
        source = (ROOT / 'src/DIYAmbient.Core/TelemetryFlagReader.cs').read_text()
        self.assertIn('value.GetType().IsEnum', source)
        self.assertIn('number != 0 && number != 1', source)
        self.assertIn('GetIndexParameters().Length == 0', source)

    def test_simple_ui_uses_fixed_maximum_power(self):
        source = (ROOT / 'src/DIYAmbient.Plugin/SettingsControl.cs').read_text()
        self.assertNotIn('Budget (A)', source)
        self.assertNotIn('Alimentation et câblage vérifiés', source)
        self.assertIn('next.CurrentBudgetAmps = 15.0', source)
        self.assertIn('3 × 60 LED — 180 LED', source)
        self.assertIn('5 × 60 LED — 300 LED', source)

    def test_settings_are_single_panel_with_solid_palette(self):
        source = (ROOT / 'src/DIYAmbient.Plugin/SettingsControl.cs').read_text()
        self.assertGreaterEqual(source.count('AddSwatch(palette,'), 10)
        self.assertNotIn('Foreground = Brushes.Black', source)
        self.assertNotIn('Mode simulation', source)
        self.assertNotIn('Tester gauche', source)
        self.assertNotIn('Aperçu de la sortie', source)
        self.assertNotIn('Courant modélisé', source)
        self.assertIn('Garder la dernière couleur après fermeture', source)
        self.assertIn('Enregistrer la connexion et les écrans', source)

    def test_shutdown_option_controls_final_black_frame(self):
        engine = (ROOT / 'src/DIYAmbient.Plugin/AmbientEngine.cs').read_text()
        self.assertIn('keepOnAfterExit = state.Enabled && state.Settings.KeepOnAfterExit', engine)
        self.assertIn('Disconnect(!keepOnAfterExit || FirmwareBusy)', engine)
        self.assertIn('Disconnect(true)', engine)

    def test_saved_enabled_state_profiles_and_telemetry_test(self):
        settings = (ROOT / 'src/DIYAmbient.Core/Settings.cs').read_text()
        plugin = (ROOT / 'src/DIYAmbient.Plugin/Plugin.cs').read_text()
        ui = (ROOT / 'src/DIYAmbient.Plugin/SettingsControl.cs').read_text()
        engine = (ROOT / 'src/DIYAmbient.Plugin/AmbientEngine.cs').read_text()
        self.assertIn('StartEnabled', settings)
        self.assertIn('if (settings.StartEnabled)', plugin)
        self.assertIn('s.StartEnabled = enabled', plugin)
        self.assertIn('Profiles.TryLoad(gameName', plugin)
        self.assertIn('data.GameName', plugin)
        self.assertIn('Configurer les zones', ui)
        self.assertIn("Tester l'effet", ui)
        self.assertIn('TestTelemetry', engine)
        self.assertIn('SendStartupBlackout', engine)
        self.assertIn('XmlLanguage.GetLanguage("fr-FR")', ui)

    def test_wled_style_animations_are_integrated_without_network_runtime(self):
        settings = (ROOT / 'src/DIYAmbient.Core/Settings.cs').read_text()
        effects = (ROOT / 'src/DIYAmbient.Core/AnimatedEffects.cs').read_text()
        ui = (ROOT / 'src/DIYAmbient.Plugin/SettingsControl.cs').read_text()
        self.assertIn('LightingMode { White, Solid, Screen, Animation, Rpm }', settings)
        for name in ('Colorloop', 'Rainbow', 'FireFlicker', 'Loading'):
            self.assertIn(name, effects)
        for removed in ('Blink', 'Breathe', 'Wipe', 'Scan', 'Theater', 'Chase', 'Twinkle'):
            self.assertNotIn(removed, effects)
        self.assertIn('Animations inspirées de WLED', ui)
        self.assertIn('AnimationSpeed', ui)
        self.assertIn('Saturation : ', ui)
        self.assertIn('Scintillement : ', ui)
        self.assertIn('Fondu : ', ui)
        self.assertNotIn('Intensité / largeur', ui)
        self.assertIn('Cycle aléatoire des couleurs', ui)
        self.assertIn('settings.SelectAnimation(', ui)
        self.assertIn('s.SolidR = 255; s.SolidG = 160; s.SolidB = 0', ui)


if __name__ == '__main__':
    print('STATIC CHECKS ONLY — C# compilation/execution and hardware validation NOT performed.', flush=True)
    unittest.main(verbosity=2)
