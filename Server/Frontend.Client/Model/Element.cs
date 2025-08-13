namespace Frontend.Client.Model
{
    public class Element
    {
        public long ActivityId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public double Distance { get; set; }  // in kilometers
        public TimeSpan Duration { get; set; }  // duration of the ride
        public double AverageSpeed { get; set; }  // km/h
        public int ElevationGain { get; set; }  // in meters
    }
}
