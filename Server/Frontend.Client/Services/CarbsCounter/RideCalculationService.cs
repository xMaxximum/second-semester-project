using Frontend.Client.Models.CarbsCounter;

namespace Frontend.Client.Services.CarbsCounter;

public class RideCalculationService
{
    public string CalculateRideDuration(double distance, double averageSpeed)
    {
        if (distance <= 0 || averageSpeed <= 0) return "0:00";
        var hours = distance / averageSpeed;
        var timeSpan = TimeSpan.FromHours(hours);
        return $"{(int)timeSpan.TotalHours}:{timeSpan.Minutes:D2}";
    }

    public TimeSpan CalculateRideDurationTimeSpan(double distance, double averageSpeed)
    {
        if (distance <= 0 || averageSpeed <= 0) return TimeSpan.Zero;
        return TimeSpan.FromHours(distance / averageSpeed);
    }

    public double CalculateDistanceFromTime(TimeSpan duration, double defaultSpeed = 25)
    {
        return duration.TotalHours * defaultSpeed;
    }

    public TimeSpan ParseDuration(string input)
    {
        if (string.IsNullOrEmpty(input)) return TimeSpan.Zero;
        
        var parts = input.Split(':');
        if (parts.Length != 2) return TimeSpan.Zero;
        
        if (int.TryParse(parts[0], out var hours) && int.TryParse(parts[1], out var minutes))
        {
            return new TimeSpan(hours, minutes, 0);
        }
        
        return TimeSpan.Zero;
    }

    public bool IsInputValid(int activeTab, double rideDistance, double averageSpeed, 
                            string rideDurationInput, double bodyWeight)
    {
        if (activeTab == 0)
        {
            return rideDistance > 0 && averageSpeed > 0 && bodyWeight > 0;
        }
        else
        {
            return !string.IsNullOrEmpty(rideDurationInput) && 
                   ParseDuration(rideDurationInput) != TimeSpan.Zero && 
                   bodyWeight > 0;
        }
    }
}
