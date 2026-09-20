// Compile the MODIFIED original sketch itself, not a separate replacement parser.
#include "evo_fake_arduino/FastLED.h"
#include <assert.h>
#include <stdio.h>
uint32_t testNow = 0;
SerialDouble Serial;
FastLedDouble FastLED;
#include "../firmware/Adalight_WS2812/Adalight_WS2812.ino"
std::vector<uint8_t> packet() {
    std::vector<uint8_t> p = {'A','d','a',0,59,59^0x55};
    for(int j=0;j<180;++j) p.push_back(uint8_t(j+1));
    return p;
}
void run(std::vector<uint8_t> p) {
    Serial.input=p; Serial.position=0;
    while(Serial.position<Serial.input.size()) loop();
}
void reset() {
    Serial=SerialDouble(); FastLED=FastLedDouble();testNow=0;
    frameActive=keepOnAfterExit=false;lastFrameAt=0;i=hi=lo=chk=0;setup();
}
bool black() {
    for(auto c:FastLED.frames.back()) if(c.r||c.g||c.b) return false;
    return true;
}
int main() {
    reset(); assert(black());assert(FastLED.frames.size()==1);assert(Serial.baud==115200 && Serial.reply=="Ada\n");
    run(packet());assert(!black());
    for(int j=0;j<60;++j) { auto c=FastLED.frames.back()[j];assert(c.r==j*3+1 && c.g==j*3+2 && c.b==j*3+3); }
    testNow=lastFrameAt+999;checkTimeout();assert(!black());
    testNow=lastFrameAt+1000;checkTimeout();assert(black());
    for(int cut=0;cut<186;++cut) {
        reset();run(packet());uint32_t last=lastFrameAt;
        auto p=packet();p.resize(cut);
        testNow=last+500;run(p);assert(!black());
        assert(FastLED.frames.size()==2);
        testNow=last+1000;checkTimeout();assert(black());
        run(packet());assert(!black());
    }
    reset();run(packet());run({'E','v','o',2,1,2^1^0xa7});assert(keepOnAfterExit);
    testNow+=10000;checkTimeout();assert(!black());run(packet());assert(!keepOnAfterExit);
    testNow=lastFrameAt+1000;checkTimeout();assert(black());
    reset();run({'E','v','o',2,1,2^1^0xa7});assert(!keepOnAfterExit);
    run({'E','v','o',1,0,1^0xa7});assert(Serial.reply=="Ada\nDIYAMBIENT-EVO/1\n");
    run(packet());run({'E','v','o',2,1,0});assert(!keepOnAfterExit);
    reset();testNow=0xfffffe00u;run(packet());testNow=lastFrameAt+1000;checkTimeout();assert(black());
    reset();run(packet());run(std::vector<uint8_t>(1200,'A'));assert(black());
    reset();auto invalid=packet();invalid[4]=39;invalid[5]=39^0x55;run(invalid);assert(black());assert(!frameActive);
    reset();run(packet());testNow=lastFrameAt+950;run(packet());assert(black());run(packet());assert(!black());
    reset();run({'A','A'});run(packet());assert(!black());
    puts("PASS modified original sketch: boot black, RGB order, deadline, 186 truncations/recovery, hold/rearm, identity, invalid controls/count, clock wrap, noise, timeout mid-frame. No hardware exercised.");
}
