#pragma once

// magnet sensor positive flank recognition (rpm)
int lastState = LOW, currentState, flankCount = 0, rpm, speed;

// calculate the speed based on rpm of the wheel with the magnet sensor
void getSpeed();
