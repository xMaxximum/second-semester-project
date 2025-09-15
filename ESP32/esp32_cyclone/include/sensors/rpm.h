#pragma once

// input pin pullup, use interrupt
void setupRPM();
// calculate the speed based on ticks per given time or time between ticks depending on the current rpm
// hybrid solution to get the most out of the two scenarios
float calculateSpeed();
