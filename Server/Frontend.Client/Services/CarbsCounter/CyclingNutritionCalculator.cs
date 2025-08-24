using Frontend.Client.Models.CarbsCounter;

namespace Frontend.Client.Services.CarbsCounter;

public class CyclingNutritionCalculator
{
    public NutritionPlan CalculateNutritionPlan(RideConfiguration config, NutritionPreferences preferences)
    {
        var plan = new NutritionPlan();
        
        // Calculate base requirements
        var carbsPerHour = CalculateCarbsPerHour(config);
        var fluidsPerHour = CalculateFluidsPerHour(config, preferences);
        
        plan.TotalCarbs = carbsPerHour * config.Duration.TotalHours;
        plan.TotalFluids = fluidsPerHour * config.Duration.TotalHours;
        plan.TotalCalories = plan.TotalCarbs * 4; // 4 kcal per gram of carbs
        
        // Generate timeline
        GenerateTimeline(plan, config, preferences, carbsPerHour, fluidsPerHour);
        
        // Generate shopping list
        GenerateShoppingList(plan, config, preferences);
        
        return plan;
    }

    private double CalculateCarbsPerHour(RideConfiguration config)
    {
        var baseCarbs = config.FitnessLevel switch
        {
            FitnessLevel.Beginner => 30.0,
            FitnessLevel.Intermediate => 45.0,
            FitnessLevel.Advanced => 60.0,
            FitnessLevel.Elite => 80.0,
            _ => 45.0
        };

        // Adjust for intensity
        baseCarbs *= config.IntensityFactor;
        
        // Adjust for body weight
        baseCarbs *= (config.BodyWeight / 70.0);
        
        return Math.Max(20, Math.Min(90, baseCarbs));
    }

    private double CalculateFluidsPerHour(RideConfiguration config, NutritionPreferences preferences)
    {
        var baseFluid = 0.5; // liters per hour
        
        // Adjust for intensity and body weight
        baseFluid *= config.IntensityFactor;
        baseFluid *= (config.BodyWeight / 70.0);
        baseFluid *= preferences.FluidIntensity;
        
        return Math.Max(0.3, Math.Min(1.2, baseFluid));
    }

    private void GenerateTimeline(NutritionPlan plan, RideConfiguration config, NutritionPreferences preferences, double carbsPerHour, double fluidsPerHour)
    {
        var timeline = new List<NutritionTimelineItem>();
        
        // Pre-ride (2-3 hours before)
        timeline.Add(new NutritionTimelineItem
        {
            Time = "2-3h before",
            Description = "Pre-ride meal",
            Instructions = "Eat a carb-rich meal with some protein. Avoid high fiber or fatty foods.",
            Type = NutritionItemType.PreRide,
            Carbs = 100,
            Calories = 400,
            SortOrder = -3
        });

        // Pre-ride hydration
        timeline.Add(new NutritionTimelineItem
        {
            Time = "2h before",
            Description = "Initial hydration",
            Instructions = "Start hydrating. Drink slowly and steadily.",
            Type = NutritionItemType.Hydration,
            Fluids = 0.5,
            SortOrder = -2
        });

        // Start of ride
        timeline.Add(new NutritionTimelineItem
        {
            Time = "0:00 (Start)",
            Description = "Begin ride",
            Instructions = "Start with well-hydrated state. Have water bottles ready.",
            Type = NutritionItemType.PreRide,
            SortOrder = 0
        });

        // During ride - real-life cadence: carbs every 45 min, hydration every 20 min
        {
            var rideDuration = config.Duration;
            var foodBuffer = TimeSpan.FromMinutes(15); // last food at least 15 min before finish

            // Carb events at 00:45, 01:30, 02:15, ... but not within last 15 minutes
            var carbTimes = new List<TimeSpan>();
            var carbStep = TimeSpan.FromMinutes(45);
            for (var t = carbStep; t <= rideDuration - foodBuffer; t += carbStep)
            {
                carbTimes.Add(t);
            }

            // Hydration events at 00:20, 00:40, 01:00, 01:20, ... up to ride end
            var drinkTimes = new List<TimeSpan>();
            var drinkStep = TimeSpan.FromMinutes(20);
            for (var d = drinkStep; d <= rideDuration; d += drinkStep)
            {
                drinkTimes.Add(d);
            }

            // Distribute totals evenly across events so timeline sums match targets
            var totalCarbs = carbsPerHour * rideDuration.TotalHours;
            var totalFluids = fluidsPerHour * rideDuration.TotalHours;

            var carbsPerEvent = carbTimes.Count > 0 ? totalCarbs / carbTimes.Count : 0.0;
            var fluidsPerEvent = drinkTimes.Count > 0 ? totalFluids / drinkTimes.Count : 0.0;

            // Add carb events
            for (int i = 0; i < carbTimes.Count; i++)
            {
                var timePoint = carbTimes[i];
                var carbSource = GetCarbSource(preferences, i + 1);
                timeline.Add(new NutritionTimelineItem
                {
                    Time = $"{timePoint.Hours}:{timePoint.Minutes:D2}",
                    Description = carbSource.Name,
                    Instructions = $"{carbSource.Instructions} Target ~{carbsPerEvent:F0}g at this stop.",
                    Type = NutritionItemType.Carbs,
                    Carbs = carbsPerEvent,
                    Calories = carbsPerEvent * 4,
                    SortOrder = (int)timePoint.TotalMinutes
                });
            }

            // Add hydration events
            for (int i = 0; i < drinkTimes.Count; i++)
            {
                var timePoint = drinkTimes[i];
                var fluidSource = GetFluidSource(preferences);
                timeline.Add(new NutritionTimelineItem
                {
                    Time = $"{timePoint.Hours}:{timePoint.Minutes:D2}",
                    Description = fluidSource.Name,
                    Instructions = $"{fluidSource.Instructions} Aim ~{fluidsPerEvent:F2}L at this mark.",
                    Type = NutritionItemType.Hydration,
                    Fluids = fluidsPerEvent,
                    SortOrder = (int)timePoint.TotalMinutes + 1 // ensure hydration after carbs if on same minute
                });
            }
        }

        // Post-ride
        timeline.Add(new NutritionTimelineItem
        {
            Time = "Within 30min",
            Description = "Recovery nutrition",
            Instructions = "Consume carbs and protein in 3:1 ratio for optimal recovery.",
            Type = NutritionItemType.PostRide,
            Carbs = 50,
            Calories = 250,
            SortOrder = 1000
        });

        plan.Timeline = timeline.OrderBy(t => t.SortOrder).ToList();
    }

    private void GenerateShoppingList(NutritionPlan plan, RideConfiguration config, NutritionPreferences preferences)
    {
        var shoppingList = new List<ShoppingListItem>();
        var totalHours = config.Duration.TotalHours;
        var totalCarbsNeeded = plan.TotalCarbs;
        
        // Define carb content per item (grams)
        var carbValues = new Dictionary<string, double>
        {
            ["gel"] = 25,        // Energy gel: ~25g carbs
            ["banana"] = 27,     // 1 medium banana: ~27g carbs  
            ["bar"] = 30,        // Energy bar: ~30g carbs
            ["dates"] = 18,      // 3 dates: ~18g carbs
            ["sportsDrink"] = 30 // Per 500ml: ~30g carbs (was 15g per 250ml)
        };

        // Priority system: determine what user actually wants
        var prioritizedSources = new List<string>();
        
        if (preferences.PreferNaturalFoods)
        {
            // Natural foods first
            if (preferences.AllowBananas) prioritizedSources.Add("banana");
            if (preferences.AllowDates) prioritizedSources.Add("dates");
            // Only add processed if no natural options
            if (!prioritizedSources.Any())
            {
                if (preferences.AllowSportsDrinks) prioritizedSources.Add("sportsDrink");
                if (preferences.AllowGels) prioritizedSources.Add("gel");
                if (preferences.AllowEnergyBars) prioritizedSources.Add("bar");
            }
        }
        else
        {
            // Balanced approach - mix of different sources for variety
            if (preferences.AllowGels) prioritizedSources.Add("gel");
            if (preferences.AllowBananas) prioritizedSources.Add("banana");
            if (preferences.AllowEnergyBars) prioritizedSources.Add("bar");
            if (preferences.AllowDates) prioritizedSources.Add("dates");
            if (preferences.AllowSportsDrinks) prioritizedSources.Add("sportsDrink");
        }

        // Fallback if nothing selected
        if (!prioritizedSources.Any())
        {
            prioritizedSources.Add("sportsDrink");
        }

        // Smart distribution: fill needs efficiently
        var carbsRemaining = totalCarbsNeeded;
        var sourceIndex = 0;
        
        while (carbsRemaining > 10 && sourceIndex < prioritizedSources.Count * 3) // max 3 rounds
        {
            var source = prioritizedSources[sourceIndex % prioritizedSources.Count];
            var carbsPerItem = carbValues[source];
            
            // Calculate how many we need of this item
            var desiredCarbsFromThisSource = Math.Min(carbsRemaining, carbsPerItem * 2); // max 2 units per round
            var quantity = Math.Max(1, Math.Ceiling(desiredCarbsFromThisSource / carbsPerItem));
            
            // Add to shopping list
            var existingItem = shoppingList.FirstOrDefault(item => 
                (source == "sportsDrink" && item.Item.ToLower().Contains("sports")) ||
                (source != "sportsDrink" && item.Item.ToLower().Contains(source)));
            
            if (existingItem != null)
            {
                // Update existing quantity - parse carefully to handle different formats
                var amountParts = existingItem.Amount.Split(' ');
                if (amountParts.Length > 0 && double.TryParse(amountParts[0], out var currentQty))
                {
                    var newQty = currentQty + quantity;
                    existingItem.Amount = source == "sportsDrink" 
                        ? $"{newQty:F1}L" 
                        : source == "dates" 
                            ? $"{(int)(newQty * 3)} pieces" 
                            : $"{(int)newQty} pieces";
                }
            }
            else
            {
                // Add new item
                switch (source)
                {
                    case "gel":
                        shoppingList.Add(new ShoppingListItem { Item = "Energy Gels", Amount = $"{quantity} pieces" });
                        break;
                    case "banana":
                        shoppingList.Add(new ShoppingListItem { Item = preferences.PreferNaturalFoods ? "🍌 Organic Bananas" : "Bananas", Amount = $"{quantity} pieces" });
                        break;
                    case "bar":
                        shoppingList.Add(new ShoppingListItem { Item = "Energy Bars", Amount = $"{quantity} pieces" });
                        break;
                    case "dates":
                        var dateServings = quantity * 3; // 3 dates per serving
                        shoppingList.Add(new ShoppingListItem { Item = preferences.PreferNaturalFoods ? "🌴 Medjool Dates" : "Dates", Amount = $"{dateServings} pieces" });
                        break;
                    case "sportsDrink":
                        var drinkVolume = quantity * 0.5; // 500ml per serving for better volume
                        shoppingList.Add(new ShoppingListItem { Item = "Sports Drink", Amount = $"{drinkVolume:F1}L" });
                        break;
                }
            }
            
            carbsRemaining -= quantity * carbsPerItem;
            sourceIndex++;
        }

        // Sports drink volume based on hydration cadence (every 20 min ~ 0.1L per sip)
        if (preferences.AllowSportsDrinks)
        {
            var drinkEvents = Math.Floor(config.Duration.TotalMinutes / 20.0);
            var sipSizeL = 0.1; // 100 ml per sip
            var baseVolumeBySips = Math.Min(plan.TotalFluids, drinkEvents * sipSizeL);
            // Ensure at least 15% of carbs can come from sports drink (30g per 500ml => 60g/L)
            var minVolumeByCarbShare = totalCarbsNeeded * 0.15 / 60.0; // liters
            var computedSportsDrinkL = Math.Max(baseVolumeBySips, minVolumeByCarbShare);
            var sportsDrinkL = Math.Round(computedSportsDrinkL, 1, MidpointRounding.AwayFromZero);

            if (sportsDrinkL > 0)
            {
                var existing = shoppingList.FirstOrDefault(s => s.Item.Contains("Sports Drink"));
                if (existing != null)
                {
                    existing.Amount = $"{sportsDrinkL:F1}L";
                }
                else
                {
                    shoppingList.Add(new ShoppingListItem { Item = "Sports Drink", Amount = $"{sportsDrinkL:F1}L" });
                }
            }
        }

        // Add hydration (separate from carb sources)
        var remainingFluids = plan.TotalFluids;
        
        // Subtract sports drink volume if already added for carbs
        var sportsDrinkItem = shoppingList.FirstOrDefault(s => s.Item.Contains("Sports Drink"));
        if (sportsDrinkItem != null)
        {
            var sportsDrinkAmount = sportsDrinkItem.Amount.Replace("L", "");
            if (double.TryParse(sportsDrinkAmount, out var sportsDrinkVolume))
            {
                remainingFluids -= sportsDrinkVolume;
            }
        }

        // Add remaining water
        if (remainingFluids > 0)
        {
            shoppingList.Add(new ShoppingListItem { Item = preferences.PreferNaturalFoods ? "Natural Spring Water" : "Water", Amount = $"{remainingFluids:F1}L" });
        }

        // Add supplements
        if (preferences.PreferNaturalFoods)
        {
            shoppingList.Add(new ShoppingListItem { Item = "Raw Honey", Amount = "250g jar" });
            if (preferences.IncludeElectrolytes)
            {
                shoppingList.Add(new ShoppingListItem { Item = "Sea Salt", Amount = "Small container" });
            }
        }
        else if (preferences.IncludeElectrolytes)
        {
            var tabCount = Math.Max(1, Math.Ceiling(totalHours));
            shoppingList.Add(new ShoppingListItem { Item = "Electrolyte Tablets", Amount = $"{tabCount} tabs" });
        }

        plan.ShoppingList = shoppingList;
    }

    private (string Name, string Instructions) GetCarbSource(NutritionPreferences preferences, int hourMark)
    {
        // Get available sources based on preferences
        var availableSources = new List<(string name, string instructions)>();
        
        if (preferences.PreferNaturalFoods)
        {
            // Natural foods only
            if (preferences.AllowBananas)
                availableSources.Add(("1 Banana", "Easy to digest and provides quick energy. Eat slowly."));
            
            if (preferences.AllowDates)
                availableSources.Add(("3-4 Dates", "Natural sugar source. Chew thoroughly and follow with water."));
            
            // Fallback for natural
            if (!availableSources.Any())
                availableSources.Add(("Honey water", "Mix 2 tbsp honey in 500ml water for natural carbs."));
        }
        else
        {
            // Processed foods allowed - add them first
            if (preferences.AllowGels)
                availableSources.Add(("Energy Gel", "Take with 150-200ml water. Don't take multiple gels at once."));

            if (preferences.AllowEnergyBars)
                availableSources.Add(("Energy Bar (half)", "Break into smaller pieces. Chew well and hydrate."));

            // Add natural options if selected
            if (preferences.AllowBananas)
                availableSources.Add(("1 Banana", "Easy to digest and provides quick energy. Eat slowly."));
            
            if (preferences.AllowDates)
                availableSources.Add(("3-4 Dates", "Natural sugar source. Chew thoroughly and follow with water."));

            // Sports drink as carb source
            if (preferences.AllowSportsDrinks)
                availableSources.Add(("Sports Drink (250ml)", "Sip regularly, provides both carbs and fluids."));
        }

        // Fallback if nothing selected
        if (!availableSources.Any())
            availableSources.Add(("Sports Drink", "Provides carbs and electrolytes. Sip regularly."));

        // Rotate through available sources to provide variety
        var sourceIndex = (hourMark - 1) % availableSources.Count;
        return availableSources[sourceIndex];
    }

    private (string Name, string Instructions) GetFluidSource(NutritionPreferences preferences)
    {
        if (preferences.AllowSportsDrinks && preferences.IncludeElectrolytes)
            return ("Sports Drink with Electrolytes", "Sip every 15-20 minutes. Don't wait until thirsty.");
        
        if (preferences.AllowSportsDrinks)
            return ("Sports Drink", "Provides carbs and some sodium. Alternate with plain water.");

        if (preferences.IncludeElectrolytes)
            return ("Water with Electrolyte Tab", "Dissolve tablet completely. Sip regularly.");

        return ("Water", "Plain water. Consider adding a pinch of salt for longer rides.");
    }
}
