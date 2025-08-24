# Cycling Carbs Calculator

## Overview

The Cycling Carbs Calculator is a comprehensive nutrition planning tool designed to help cyclists optimize their nutrition strategy for rides of varying durations and intensities. The component provides personalized carbohydrate and hydration recommendations based on individual rider characteristics, ride parameters, and dietary preferences.

## Features

### 🚴‍♂️ Ride Configuration
- **Dual Input Modes**: Configure rides by distance/speed or by time duration
- **Intensity Factor**: Adjust calculations based on ride intensity (0.5 - 1.0)
- **Personal Settings**: Body weight and fitness level customization
- **Real-time Validation**: Input validation with immediate feedback

### 🍌 Nutrition Preferences
- **Food Source Selection**: Choose from energy gels, sports drinks, bananas, energy bars, and dates
- **Natural vs. Processed**: Option to prefer natural food sources
- **Hydration Customization**: Electrolyte preferences and fluid intensity settings
- **Weather Adaptation**: Hydration intensity adjustment for different conditions

### 📊 Personalized Results
- **Summary Cards**: Total carbs, fluids, and calories needed
- **Nutrition Timeline**: Time-based feeding schedule with specific recommendations
- **Shopping List**: Automatically generated shopping list with quantities
- **Visual Timeline**: Color-coded timeline showing pre-ride, during-ride, and post-ride nutrition

## Architecture

The component follows a clean architecture pattern with separation of concerns:

```
Frontend.Client/
├── Pages/
│   └── CarbsCounter.razor                    # Main page component
├── Components/CarbsCounter/
│   ├── RideConfigurationComponent.razor      # Input form component
│   └── NutritionResultsComponent.razor       # Results display component
├── Models/CarbsCounter/
│   └── NutritionModels.cs                    # Data models and enums
├── Services/CarbsCounter/
│   ├── CyclingNutritionCalculator.cs         # Core calculation engine
│   ├── RideCalculationService.cs             # Ride parameter calculations
│   └── NutritionDisplayHelper.cs             # UI helper methods
└── wwwroot/css/
    └── carbscounter.css                       # Component-specific styles
```

## Component Structure

### Models (`NutritionModels.cs`)
- **NutritionPreferences**: User dietary preferences and restrictions
- **RideConfiguration**: Ride parameters (duration, distance, intensity, etc.)
- **NutritionPlan**: Complete nutrition plan with timeline and shopping list
- **NutritionTimelineItem**: Individual nutrition events with timing and instructions
- **ShoppingListItem**: Shopping list items with quantities
- **Enums**: `FitnessLevel`, `NutritionItemType`

### Services

#### `CyclingNutritionCalculator.cs`
Core calculation engine that:
- Calculates carbs per hour based on fitness level, intensity, and body weight
- Generates hydration requirements with weather adjustments
- Creates detailed nutrition timeline with 45-minute carb intervals
- Builds intelligent shopping lists based on user preferences

#### `RideCalculationService.cs`
Utility service for:
- Converting between distance/speed and time duration
- Input validation
- Time parsing and formatting

#### `NutritionDisplayHelper.cs`
UI helper service providing:
- Color coding for different nutrition types
- Icon mapping for food items
- Nutrient information formatting

### Components

#### `RideConfigurationComponent.razor`
Input form component featuring:
- Tabbed interface for distance vs. time input
- Personal settings configuration
- Nutrition preferences panel
- Real-time validation and feedback

#### `NutritionResultsComponent.razor`
Results display component with:
- Summary cards with gradient backgrounds
- Interactive timeline with MudBlazor timeline component
- Shopping list with categorized items and icons

## Usage

### Basic Usage
1. Navigate to `/carbscounter`
2. Configure ride parameters (distance/speed or duration)
3. Set personal information (weight, fitness level)
4. Adjust nutrition preferences
5. Click "Calculate Nutrition Plan"

### Advanced Configuration
- **Intensity Factor**: 
  - 0.5-0.6: Easy/Recovery rides
  - 0.7-0.8: Moderate/Tempo rides
  - 0.85-1.0: Hard/Race efforts
- **Hydration Intensity**:
  - 0.7: Cool weather conditions
  - 1.0: Normal conditions
  - 1.5: Hot weather/high sweat rate

## Calculation Logic

### Carbohydrate Requirements
Base carbohydrate needs are determined by fitness level:
- **Beginner**: 30g/hour
- **Intermediate**: 45g/hour
- **Advanced**: 60g/hour
- **Elite**: 80g/hour

Adjustments are made for:
- Intensity factor (multiplier)
- Body weight (normalized to 70kg baseline)
- Final range: 20-90g/hour

### Hydration Requirements
Base fluid intake: 0.5L/hour, adjusted for:
- Intensity factor
- Body weight
- Weather conditions (fluid intensity)
- Final range: 0.3-1.2L/hour

### Timeline Generation
- **Pre-ride**: 2-3 hours before (meal) and 2 hours before (hydration)
- **During ride**: 
  - Carbohydrates every 45 minutes
  - Hydration every 20 minutes
  - No food within 15 minutes of finish
- **Post-ride**: Recovery nutrition within 30 minutes