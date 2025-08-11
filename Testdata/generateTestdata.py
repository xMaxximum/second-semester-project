import json
import random
from datetime import timedelta
import math

def haversine(lat1, lon1, lat2, lon2):
    # Calculate distance in meters between two lat/lon points
    R = 6371000  # Earth radius in meters
    phi1 = math.radians(lat1)
    phi2 = math.radians(lat2)
    dphi = math.radians(lat2 - lat1)
    dlambda = math.radians(lon2 - lon1)
    a = math.sin(dphi/2)**2 + math.cos(phi1)*math.cos(phi2)*math.sin(dlambda/2)**2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1-a))
    return R * c

def generate_data_point(t, last_coords, coord_update_counter, last_speed):
    # Simulate temperature (20-30°C)
    temperature = round(random.uniform(20, 30), 1)
    peak_acc = [round(random.uniform(-5, 5), 2) for _ in range(3)]

    # Update coordinates every 1.2-3s
    if coord_update_counter <= 0:
        # Move always north (latitude increases), longitude stays the same
        delta_lat = random.uniform(0.00001, 0.0001)  # always positive
        latitude = round(last_coords['latitude'] + delta_lat, 6)
        longitude = last_coords['longitude']
        height = last_coords['height'] + random.randint(-1, 1)
        coords = {"latitude": latitude, "longitude": longitude, "height": height}
        next_update = random.randint(4, 15)  # 0.8s to 3s at 200ms intervals

        # Calculate speed based on distance and time
        distance = haversine(last_coords['latitude'], last_coords['longitude'], latitude, longitude)
        # Spread distance over the interval (next_update * 0.2s)
        speed = round(distance / (next_update * 0.2), 2)
        # Clamp speed to max 11.11 m/s (40 km/h)
        speed = min(speed, 11.11)
    else:
        coords = last_coords
        next_update = coord_update_counter - 1
        speed = last_speed

    # Calculate checksum as sum of all numeric values
    checksum = (
        temperature + speed +
        coords['latitude'] + coords['longitude'] + coords['height'] + sum(peak_acc)
    )

    

    return {
        "current_temperature": temperature,
        "current_speed": speed,
        "current_coordinates": coords,
        "peak_acceleration_x": peak_acc[0],
        "peak_acceleration_y": peak_acc[1],
        "peak_acceleration_z": peak_acc[2],
        "checksum": round(checksum, 6)
    }, coords, next_update, speed

def main():
    data = []
    coords = {"latitude": 51.123456, "longitude": 8.123456, "height": 455}
    coord_update_counter = 0
    speed = 0.0
    
    for t in range(9000):  # 30 minutes at 5Hz
        point, coords, coord_update_counter, speed = generate_data_point(t, coords, coord_update_counter, speed)
        data.append(point)

    with open("./testdata.json", "w") as f:
        json.dump(data, f, indent=4)

if __name__ == "__main__":
    main()