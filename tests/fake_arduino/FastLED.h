// HOST TEST DOUBLE ONLY. Never compile/upload this file for an Arduino.
// No timing, interrupts, USB, power, or LED electrical behavior is simulated.
#pragma once
#include <cstdint>
#include <cstring>
#include <stdexcept>
#include <vector>
#include <string>
using byte = uint8_t;
struct NeedInput {};
struct CRGB {
    uint8_t r, g, b;
    CRGB() : r(0), g(0), b(0) {}
    CRGB(uint8_t red, uint8_t green, uint8_t blue) : r(red), g(green), b(blue) {}
};
struct NEOPIXEL {};
struct SerialDouble {
    std::vector<uint8_t> input;
    size_t position = 0;
    unsigned baud = 0;
    std::string startup;
    void begin(unsigned rate) { baud = rate; }
    void print(const char* s) { startup += s; }
    size_t available() {
        if (position == input.size()) throw NeedInput();
        return input.size() - position;
    }
    int read() { return input.at(position++); }
};
struct FastLedDouble {
    CRGB* strip = nullptr;
    size_t count = 0;
    std::vector<std::vector<CRGB>> frames;
    template <typename CHIP, int PIN> void addLeds(CRGB* leds, size_t length) {
        strip = leds; count = length;
    }
    void show() { frames.emplace_back(strip, strip + count); }
    void showColor(CRGB color) {
        for (size_t i = 0; i < count; ++i) strip[i] = color;
        show();
    }
};
extern SerialDouble Serial;
extern FastLedDouble FastLED;
#define LEDS FastLED
inline void delay(unsigned) {}
