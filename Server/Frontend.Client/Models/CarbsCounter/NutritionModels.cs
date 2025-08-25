namespace Frontend.Client.Models.CarbsCounter;

public class NutritionPreferences
{
    public bool AllowGels { get; set; } = true;
    public bool AllowSportsDrinks { get; set; } = true;
    public bool AllowBananas { get; set; }
    public bool AllowEnergyBars { get; set; } = true;
    public bool AllowDates { get; set; }
    public bool PreferNaturalFoods { get; set; }
    public bool IncludeElectrolytes { get; set; }
    public double FluidIntensity { get; set; } = 1.0;
}

public class RideConfiguration
{
    public TimeSpan Duration { get; set; }
    public double Distance { get; set; }
    public double IntensityFactor { get; set; }
    public double BodyWeight { get; set; }
    public FitnessLevel FitnessLevel { get; set; }
}

public class NutritionPlan
{
    public double TotalCarbs { get; set; }
    public double TotalFluids { get; set; }
    public double TotalCalories { get; set; }
    public List<NutritionTimelineItem> Timeline { get; set; } = new();
    public List<ShoppingListItem> ShoppingList { get; set; } = new();
}

public class NutritionTimelineItem
{
    public string Time { get; set; } = "";
    public string Description { get; set; } = "";
    public string Instructions { get; set; } = "";
    public NutritionItemType Type { get; set; }
    public double Carbs { get; set; }
    public double Fluids { get; set; }
    public double Calories { get; set; }
    public int SortOrder { get; set; }
}

public class ShoppingListItem
{
    public string Item { get; set; } = "";
    public string Amount { get; set; } = "";
}

public enum NutritionItemType
{
    PreRide,
    Carbs,
    Hydration,
    PostRide
}

public enum FitnessLevel
{
    Beginner,
    Intermediate,
    Advanced,
    Elite
}
