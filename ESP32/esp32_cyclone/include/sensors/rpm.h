#ifndef RPM_H
#define RPM_H

// magnet sensor positive flank recognition (rpm)
int lastState = LOW, currentState, flankCount = 0, rpm, speed;

// calculate the speed based on rpm of the wheel with the magnet sensor
void getSpeed();


#endif