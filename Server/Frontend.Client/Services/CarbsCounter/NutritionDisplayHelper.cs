using Frontend.Client.Models.CarbsCounter;
using MudBlazor;

namespace Frontend.Client.Services.CarbsCounter;

public static class NutritionDisplayHelper
{
    public static Color GetTimelineColor(NutritionItemType type)
    {
        return type switch
        {
            NutritionItemType.PreRide => Color.Info,
            NutritionItemType.Carbs => Color.Primary,
            NutritionItemType.Hydration => Color.Secondary,
            NutritionItemType.PostRide => Color.Success,
            _ => Color.Default
        };
    }

    public static string GetNutrientInfo(NutritionTimelineItem item)
    {
        return item.Type switch
        {
            NutritionItemType.Carbs => $"{(item.Carbs % 1 == 0 ? item.Carbs.ToString("F0") : item.Carbs.ToString("F2"))}g",
            NutritionItemType.Hydration => $"{item.Fluids:F1}L",
            _ => item.Calories > 0 ? $"{(item.Calories % 1 == 0 ? item.Calories.ToString("F0") : item.Calories.ToString("F2"))}kcal" : "Info"
        };
    }

    public static string GetItemIcon(string item)
    {
        var lowerItem = item.ToLower();
        
        if (lowerItem.Contains("gel")) return Icons.Material.Filled.Science;
        if (lowerItem.Contains("drink") || lowerItem.Contains("sports")) return Icons.Material.Filled.LocalDrink;
        if (lowerItem.Contains("banana")) return Icons.Custom.Uncategorized.FoodApple;
        if (lowerItem.Contains("bar")) return Icons.Material.Filled.Rectangle;
        if (lowerItem.Contains("date")) return Icons.Custom.Uncategorized.FoodApple;
        if (lowerItem.Contains("water")) return Icons.Material.Filled.WaterDrop;
        if (lowerItem.Contains("honey")) return Icons.Custom.Uncategorized.FoodApple;
        if (lowerItem.Contains("electrolyte") || lowerItem.Contains("tablet")) return Icons.Material.Filled.MedicalServices;
        
        return Icons.Material.Filled.Fastfood;
    }
}
