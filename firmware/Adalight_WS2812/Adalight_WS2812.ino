/*
 * Arduino interface for the use of WS2812 strip LEDs
 * Uses Adalight protocol and is compatible with Boblight, Prismatik etc...
 * "Magic Word" for synchronisation is 'Ada' followed by LED High, Low and Checksum
 * @author: Wifsimster <wifsimster@gmail.com>
 * @library: FastLED v3.001
 * @date: 11/22/2015
 * EVO maintenance: bounded waits, lost-frame blackout and optional shutdown hold.
 */
#include "FastLED.h"
#define NUM_LEDS 60
#define DATA_PIN 6

// Baudrate, higher rate allows faster refresh rate and more LEDs (defined in /etc/boblight.conf)
#define serialRate 115200

// Adalight sends a "Magic Word" (defined in /etc/boblight.conf) before sending the pixel data
uint8_t prefix[] = {'A', 'd', 'a'}, hi, lo, chk, i;

// Initialise LED-array
CRGB leds[NUM_LEDS];

// EVO: only a complete frame refreshes this timeout. No EEPROM writes.
const uint32_t frameTimeoutMs = 1000, byteTimeoutMs = 100;
uint32_t lastFrameAt = 0;
bool frameActive = false, keepOnAfterExit = false;

void checkTimeout() {
  if (frameActive && !keepOnAfterExit && uint32_t(millis() - lastFrameAt) >= frameTimeoutMs) {
    LEDS.showColor(CRGB(0, 0, 0));
    frameActive = false;
  }
}

// Replace the original infinite waits, keeping its header/RGB receive loops.
bool readByte(uint8_t &value) {
  uint32_t started = millis();
  do {
    bool wasActive = frameActive;
    checkTimeout();
    if (wasActive && !frameActive) return false; // Blackout invalidates a partial RGB buffer.
    if (Serial.available()) { value = Serial.read(); return true; }
  } while (uint32_t(millis() - started) < byteTimeoutMs);
  return false;
}

// Optional EVO control, only recognized while searching for the Adalight prefix.
void readControl() {
  uint8_t v, o, command, argument, checksum;
  if (!readByte(v) || v != 'v' || !readByte(o) || o != 'o') return;
  if (!readByte(command) || !readByte(argument) || !readByte(checksum)) return;
  if (checksum != uint8_t(command ^ argument ^ 0xa7)) return;
  if (command == 1 && argument == 0) Serial.print("DIYAMBIENT-EVO/1\n");
  if (command == 2 && argument == 1 && frameActive) keepOnAfterExit = true;
}

void setup() {
  // Use NEOPIXEL to keep true colors
  FastLED.addLeds<NEOPIXEL, DATA_PIN>(leds, NUM_LEDS);

  // EVO: start black, without the original full-power RGB flashes.
  LEDS.showColor(CRGB(0, 0, 0));

  Serial.begin(serialRate);
  // Send "Magic Word" string to host
  Serial.print("Ada\n");
}

void loop() {
  uint8_t value;
  // Wait for first byte of Magic Word
  for(i = 0; i < sizeof prefix; ++i) {
    waitLoop: if (!readByte(value)) return;
    // Check next byte in Magic Word
    if(prefix[i] == value) continue;
    // EVO commands cannot be interpreted inside RGB pixel data.
    if (value == 'E') { readControl(); return; }
    // otherwise, start over; preserve a new 'A' as the start of a prefix.
    i = value == prefix[0] ? 1 : 0;
    goto waitLoop;
  }

  // Hi, Lo, Checksum
  if (!readByte(hi) || !readByte(lo) || !readByte(chk)) return;

  // If checksum/count does not match go back to wait.
  if (chk != (hi ^ lo ^ 0x55) || hi != 0 || lo != NUM_LEDS - 1) {
    i=0;
    goto waitLoop;
  }

  memset(leds, 0, NUM_LEDS * sizeof(struct CRGB));
  // Read the transmission data and set LED values
  for (uint8_t i = 0; i < NUM_LEDS; i++) {
    byte r, g, b;
    if (!readByte(r) || !readByte(g) || !readByte(b)) return;
    leds[i].r = r;
    leds[i].g = g;
    leds[i].b = b;
  }

  // Shows new values only after a complete transmission.
  FastLED.show();
  lastFrameAt = millis();
  frameActive = true;
  keepOnAfterExit = false;
}
