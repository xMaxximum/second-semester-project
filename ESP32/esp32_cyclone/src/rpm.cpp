#include <constants.h>
#include <sensors/rpm.h>
#include <Arduino.h>

const uint8_t PULSES_PER_REV = 1;      // pulses (flanks) per wheel revolution
const unsigned long TIMEOUT_MS = 1500; // if no pulse within this timeout, speed -> 0

// use time between pulses under 150 rpm and pulses within 200ms window above 150 rpm
const float RPM_TO_SWITCH = 300.0;

// EMA smoothing prevents jitter values
const float EMA_ALPHA = 0.25f;

volatile unsigned long lastPulseMicros = 0;
volatile unsigned long lastPeriodMicros = 0;
volatile unsigned int pulseCount = 0;

float speedFiltered = 0.0f, speedKmh;

void IRAM_ATTR onPulse()
{
    unsigned long t = micros();
    // ignore extremely close pulses (<100us)
    if (t - lastPulseMicros < 100)
        return;
    // compute period (time since last pulse)
    if (lastPulseMicros != 0)
        lastPeriodMicros = t - lastPulseMicros;
    lastPulseMicros = t;

    // count the pulses for the evaluation in calculateSpeed method
    pulseCount++;
}

void setupRPM()
{
    // pullup because sensor pulls it down (clear signal states)
    pinMode(PIN_MAGNET, INPUT_PULLUP);
    // count pulses that are falling flanks
    attachInterrupt(digitalPinToInterrupt(PIN_MAGNET), onPulse, FALLING);
}

float calculateSpeed()
{
    float rpmInstant = 0.0f;
    noInterrupts();
    unsigned long periodUs = lastPeriodMicros;
    unsigned long lastPulseUsCopy = lastPulseMicros;
    interrupts();

    if (periodUs > 0) // not relevant for the first evaluation (rpm 0)
    {
        // periodUs is microseconds per pulse. This needs to be converted into rpm
        // RPM = (60 seconds * 1e6 microseconds) / (periodUs * pulses_per_rev)
        float periodSec = periodUs / 1e6; // seconds per pulse
        Serial.print(" secperpulse: ");
        Serial.print(periodSec);
        float pulsesPerSec = 1.0f / periodSec; // pulses per second
        Serial.print(" pulsespersec: ");
        Serial.print(pulsesPerSec);
        rpmInstant = pulsesPerSec * 60.0f / PULSES_PER_REV; // pulses per minute divided by pulses per revolution equal rpm
        //rpmInstant = (60.0f * 1000000.0f) / (float(periodUs) * float(PULSES_PER_REV));
    }

    // get the current interrupt set pulsecount
    noInterrupts();
    unsigned int cnt = pulseCount;
    pulseCount = 0;
    interrupts();

    float pulsesPerSec = float(cnt) / 0.2f;
    float rpmWindow = pulsesPerSec * 60.0f / float(PULSES_PER_REV);

    // period-based (instant) or window-based rpm.
    // period is better for slow speeds and window for high speeds (have verified this in a test, the jitter makes period bad for high speeds because then there are no smooth values)
    
    float rpmUsed;
    /*
    if (rpmInstant < RPM_TO_SWITCH && periodUs > 0)
        rpmUsed = rpmInstant;
    else
        rpmUsed = rpmWindow;
        */
    rpmUsed = rpmInstant;

    Serial.print(" rpm: ");
    Serial.print(rpmUsed);

    // speed = RPM * pi * D / 60 * 3.6
    // the speed is off by a factor of 40, I add it here because I do not know where this factor comes from, would need further debugging
    speedKmh = ((rpmUsed * 3.14159265358979323846f * WHEEL_DIAMETER) / 60.0f * 3.6f) / 40;

    // EMA smoothing, "removes" jitter values
    speedFiltered = EMA_ALPHA * speedKmh + (1.0f - EMA_ALPHA) * speedFiltered;

    // no pulses for a while
    if (millis() - (lastPulseUsCopy / 1000UL) > TIMEOUT_MS)
    {
        speedKmh = 0;
        speedFiltered = 0;
    }
    return speedFiltered;
}