// Compiles the user's UNMODIFIED .ino with explicitly simulated Serial/FastLED.
// This is parser/protocol validation, NOT C# execution or a hardware test.
#include "fake_arduino/FastLED.h"
#include <iostream>
#include <algorithm>
SerialDouble Serial;
FastLedDouble FastLED;
#include "firmware_fixture/Adalight_WS2812.original.ino"

static void require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}
static std::vector<uint8_t> packet(bool black = false) {
    std::vector<uint8_t> p = {'A','d','a',0,59,uint8_t(59 ^ 0x55)};
    for (int led = 0; led < 60; ++led) {
        p.push_back(black ? 0 : uint8_t(led));
        p.push_back(black ? 0 : uint8_t(led + 60));
        p.push_back(black ? 0 : uint8_t(led + 120));
    }
    return p;
}
static void reset() {
    Serial = SerialDouble(); FastLED = FastLedDouble();
    i = hi = lo = chk = 0;
    setup();
    FastLED.frames.clear();
}
static void run(const std::vector<uint8_t>& bytes) {
    Serial.input = bytes; Serial.position = 0;
    try { while (true) loop(); } catch (const NeedInput&) {}
}
static bool black(const std::vector<CRGB>& f) {
    return std::all_of(f.begin(), f.end(), [](const CRGB& c){ return c.r == 0 && c.g == 0 && c.b == 0; });
}
int main() {
    int passed = 0;
    try {
        reset();
        require(Serial.baud == 115200 && Serial.startup == "Ada\n", "Startup serial configuration"); ++passed;
        run(packet());
        require(FastLED.frames.size() == 1, "One frame expected");
        for (int led = 0; led < 60; ++led) {
            const auto c = FastLED.frames.back()[led];
            require(c.r == led && c.g == led + 60 && c.b == led + 120, "RGB or physical order mismatch");
        }
        ++passed;
        reset(); auto twice = packet(); auto second = packet(true);
        twice.insert(twice.end(), second.begin(), second.end()); run(twice);
        require(FastLED.frames.size() == 2 && black(FastLED.frames.back()), "Consecutive frames failed"); ++passed;
        reset(); auto shortened = packet(); shortened[4] = 39; shortened[5] = 39 ^ 0x55; shortened.resize(6 + 40 * 3);
        run(shortened); require(FastLED.frames.empty(), "Firmware unexpectedly accepted 40 colors"); ++passed;
        reset(); auto wrongCount = packet(); wrongCount[4] = 39; wrongCount[5] = 39 ^ 0x55;
        run(wrongCount); require(FastLED.frames.size() == 1, "Hardcoded 60-color behavior changed"); ++passed;
        reset(); auto bad = packet(); bad[5] ^= 1; bad.resize(6); run(bad);
        require(FastLED.frames.empty(), "Invalid checksum accepted"); ++passed;
        // Every possible truncation of an old packet followed by the startup
        // recovery bytes (186 zeros + a full black Adalight packet).
        for (size_t cut = 0; cut < 186; ++cut) {
            reset(); auto interrupted = packet(); interrupted.resize(cut);
            interrupted.insert(interrupted.end(), 186, 0);
            auto off = packet(true); interrupted.insert(interrupted.end(), off.begin(), off.end());
            run(interrupted);
            require(!FastLED.frames.empty() && black(FastLED.frames.back()), "Recovery did not end with black");
            ++passed;
        }
        reset(); FastLED.frames.clear(); setup();
        require(FastLED.frames.size() == 4, "Four startup patterns expected");
        require(FastLED.frames[0][0].r == 255 && FastLED.frames[1][0].g == 255 && FastLED.frames[2][0].b == 255,
            "Startup full-intensity RGB behavior changed");
        require(black(FastLED.frames[3]), "Startup should end black"); ++passed;
        reset(); run(packet()); const auto previous = FastLED.frames.size(); run({});
        require(FastLED.frames.size() == previous && !black(FastLED.frames.back()), "Unexpected firmware timeout/blackout"); ++passed;
        std::cout << "PASS: " << passed << " host-simulated firmware parser cases.\n";
        std::cout << "The .ino was unchanged. Serial/FastLED are test doubles. No UART timing, USB, current, physical LED, C#, WPF or SimHub validation.\n";
        return 0;
    } catch (const std::exception& ex) {
        std::cerr << "FAIL after " << passed << " cases: " << ex.what() << "\n"; return 1;
    }
}
