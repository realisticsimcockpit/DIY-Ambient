// HOST TEST DOUBLE ONLY: never upload. Clock/UART are deterministic, not electrical models.
#pragma once
#include <stdint.h>
#include <string.h>
#include <vector>
#include <string>
typedef uint8_t byte;
extern uint32_t testNow;
inline uint32_t millis() { return testNow; }
struct CRGB {
    uint8_t r,g,b;
    CRGB() : r(0),g(0),b(0) {}
    CRGB(uint8_t x,uint8_t y,uint8_t z) : r(x),g(y),b(z) {}
};
struct NEOPIXEL {};
struct SerialDouble {
    std::vector<uint8_t> input;
    size_t position = 0;
    unsigned baud = 0;
    std::string reply;
    void begin(unsigned rate) { baud = rate; }
    void print(const char* s) { reply += s; }
    size_t available() { ++testNow; return input.size() - position; }
    int read() { return input.at(position++); }
};
struct FastLedDouble {
    CRGB* strip = nullptr;
    size_t count = 0;
    std::vector<std::vector<CRGB>> frames;
    template <typename CHIP, int PIN> void addLeds(CRGB* values,size_t length) { strip=values;count=length; }
    void show() { frames.emplace_back(strip,strip+count); }
    void showColor(CRGB color) { for(size_t j=0;j<count;++j) strip[j]=color;show(); }
};
extern SerialDouble Serial;
extern FastLedDouble FastLED;
#define LEDS FastLED
