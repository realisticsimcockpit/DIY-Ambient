"""Independent reference calculations, NOT an execution of the C# plugin."""
from pathlib import Path
import json
import math
import random
import unittest

ROOT = Path(__file__).resolve().parents[1]
IDLE = 0.001
CHANNEL = 0.020
COUNT = 60


def modeled_current(frame, strips=1):
    return strips * (len(frame) * IDLE + CHANNEL * sum(sum(pixel) for pixel in frame) / 255)


def old_limit(frame, budget=0.5, brightness=1):
    dynamic = max(0, modeled_current(frame) - COUNT * IDLE)
    gain = min(1, max(0, budget - COUNT * IDLE) / dynamic) if dynamic else 1
    return [tuple(math.floor(channel * gain * brightness) for channel in pixel) for pixel in frame]


def fixed_limit(frame, budget=0.5, brightness=1, strips=1):
    gain = min(1, max(0, budget - COUNT * strips * IDLE) / (COUNT * strips * 3 * CHANNEL))
    return [tuple(math.floor(channel * gain * brightness) for channel in pixel) for pixel in frame]


def white(warmth, tint):
    gains = [1 + .25 * warmth, 1 + .25 * tint, 1 - .25 * warmth]
    largest = max(gains)
    return tuple(math.floor(255 * gain / largest) for gain in gains)


class ReferenceModels(unittest.TestCase):
    def test_reproduce_old_background_pumping(self):
        baseline = [(255, 255, 255)] * 60
        five_red = [(255, 0, 0)] * 5 + baseline[5:]
        thirty_red = [(255, 0, 0)] * 30 + baseline[30:]
        self.assertEqual(old_limit(baseline)[-1], (31, 31, 31))
        self.assertEqual(old_limit(five_red)[-1], (33, 33, 33))
        self.assertEqual(old_limit(thirty_red)[-1], (46, 46, 46))

    def test_fixed_gain_does_not_pump(self):
        for count in range(61):
            frame = [(255, 0, 0)] * count + [(255, 255, 255)] * (60 - count)
            self.assertTrue(all(pixel == (31, 31, 31) for pixel in fixed_limit(frame)[count:]))

    def test_fixed_gain_budget_10000_random_frames(self):
        rng = random.Random(20260920)
        for _ in range(10000):
            frame = [tuple(rng.randrange(256) for _ in range(3)) for _ in range(60)]
            budget = .06 + rng.random() * 4.94
            output = fixed_limit(frame, budget, rng.random())
            self.assertLessEqual(modeled_current(output), budget + 1e-9)

    def test_full_white_range_and_brightness_monotonicity(self):
        for budget in (.06, .1, .5, 1, 2, 3.66, 5):
            samples = [fixed_limit([(255, 255, 255)] * 60, budget, i / 100)[0][0] for i in range(101)]
            self.assertEqual(samples, sorted(samples))
            self.assertEqual(samples[0], 0)

    def test_three_or_five_strips_respect_15_amp_supply(self):
        frame = [(255, 255, 255)] * 60
        three = fixed_limit(frame, 15, 1, 3)
        five = fixed_limit(frame, 15, 1, 5)
        self.assertEqual(three[0], (255, 255, 255))
        self.assertLess(five[0][0], 255)
        self.assertLessEqual(modeled_current(three, 3), 15)
        self.assertLessEqual(modeled_current(five, 5), 15)

    def test_white_preset_channels_never_overdrive(self):
        for wi in range(-50, 51):
            for ti in range(-50, 51):
                self.assertTrue(all(0 <= channel <= 255 for channel in white(wi / 50, ti / 50)))
        self.assertEqual(white(0, 0), (255, 255, 255))

    def test_ranked_split_has_equal_disjoint_sides_for_all_counts(self):
        settings = json.loads((ROOT / 'docs/settings.example.json').read_text())
        roles = {d['DeviceName'].lower(): d['Position'] for d in settings['Displays']}
        order = sorted(settings['Zones'], key=lambda z: (
            roles[z['DeviceName'].lower()] + z['X'] + z['Width'] / 2,
            z['Y'] + z['Height'] / 2, z['Led']))
        ids = [z['Led'] for z in order]
        for count in range(0, 61, 2):
            left, right = ids[:count // 2], list(reversed(ids))[:count // 2]
            self.assertEqual(len(left), count // 2)
            self.assertEqual(len(right), count // 2)
            self.assertFalse(set(left) & set(right))
        self.assertTrue(all(i <= 20 for i in ids[:5]))
        self.assertTrue(all(i >= 41 for i in ids[-5:]))

    def test_adalight_contract_and_uart_lower_bound(self):
        payload = bytes((i % 256 for i in range(180)))
        packet = bytes([65, 100, 97, 0, 59, 59 ^ 0x55]) + payload
        self.assertEqual(len(packet), 186)
        self.assertEqual(packet[:6], b'Ada\x00;n')
        # Ideal 8N1 UART serialization only, NOT a measured USB/Arduino duration.
        self.assertAlmostEqual(186 * 10 / 115200, 0.016145833333333333)


if __name__ == '__main__':
    print('REFERENCE MODELS ONLY — no C# compilation or execution, no current measurement.', flush=True)
    unittest.main(verbosity=2)
