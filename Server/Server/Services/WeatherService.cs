using Microsoft.Extensions.Options;
using Shared.Models;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Server.Services
{
    public class WeatherService
    {
        private Dictionary<string, WeatherData> cachedWeatherData = new Dictionary<string, WeatherData>();
        private readonly HttpClient _http;
        private readonly string _apiKey;

        public WeatherService(HttpClient http, IOptions<ApiOptions> options)
        {
            _http = http;
            _apiKey = options.Value.WeatherApiKey;
        }

        // Method for getting weather by coordinates (if user provides exact lat/lon)
        public async Task<WeatherData> GetWeatherAsync(double lat, double lon)
        {
            var cacheKey = $"{lat:F2},{lon:F2}";
            
            if (cachedWeatherData.ContainsKey(cacheKey) &&
                cachedWeatherData.TryGetValue(cacheKey, out WeatherData cachedData))
            {
                long nowMillis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                if (nowMillis - cachedData.timestamp < 1000 * 60 * 10) // 10 minute cache
                {
                    return cachedData;
                }
            }

            var url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={_apiKey}&units=metric";
            var weather = await _http.GetFromJsonAsync<OpenWeatherResponse>(url);
            if (weather == null) throw new Exception("Weather API failed");

            WeatherData data = CreateWeatherData(weather);
            cachedWeatherData[cacheKey] = data;
            return data;
        }

        // Method for getting weather by city name (most common use case)
        public async Task<WeatherData> GetWeatherByCityAsync(string cityName)
        {
            if (string.IsNullOrWhiteSpace(cityName))
                throw new ArgumentException("City name cannot be empty", nameof(cityName));

            var cacheKey = cityName.ToLowerInvariant();
            
            if (cachedWeatherData.ContainsKey(cacheKey) &&
                cachedWeatherData.TryGetValue(cacheKey, out WeatherData cachedData))
            {
                long nowMillis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                if (nowMillis - cachedData.timestamp < 1000 * 60 * 10) // 10 minute cache
                {
                    return cachedData;
                }
            }

            var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(cityName)}&appid={_apiKey}&units=metric";
            var weather = await _http.GetFromJsonAsync<OpenWeatherResponse>(url);
            if (weather == null) throw new Exception("Weather API failed - city not found");

            WeatherData data = CreateWeatherData(weather);
            cachedWeatherData[cacheKey] = data;
            return data;
        }

        // Method for getting weather by city name and country code for better accuracy
        public async Task<WeatherData> GetWeatherByCityAndCountryAsync(string cityName, string countryCode)
        {
            if (string.IsNullOrWhiteSpace(cityName))
                throw new ArgumentException("City name cannot be empty", nameof(cityName));
            
            if (string.IsNullOrWhiteSpace(countryCode))
                throw new ArgumentException("Country code cannot be empty", nameof(countryCode));

            var cacheKey = $"{cityName.ToLowerInvariant()},{countryCode.ToUpperInvariant()}";
            
            if (cachedWeatherData.ContainsKey(cacheKey) &&
                cachedWeatherData.TryGetValue(cacheKey, out WeatherData cachedData))
            {
                long nowMillis = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                if (nowMillis - cachedData.timestamp < 1000 * 60 * 10) // 10 minute cache
                {
                    return cachedData;
                }
            }

            var url = $"https://api.openweathermap.org/data/2.5/weather?q={Uri.EscapeDataString(cityName)},{countryCode.ToUpperInvariant()}&appid={_apiKey}&units=metric";
            var weather = await _http.GetFromJsonAsync<OpenWeatherResponse>(url);
            if (weather == null) throw new Exception("Weather API failed - city/country combination not found");

            WeatherData data = CreateWeatherData(weather);
            cachedWeatherData[cacheKey] = data;
            return data;
        }

        // Geocoding method for city autocomplete
        public async Task<List<LocationSuggestion>> SearchLocationsAsync(string query, int limit = 5)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return new List<LocationSuggestion>();

            try
            {
                var url = $"https://api.openweathermap.org/geo/1.0/direct?q={Uri.EscapeDataString(query)}&limit={limit}&appid={_apiKey}";
                var results = await _http.GetFromJsonAsync<List<GeocodingResult>>(url);
                
                if (results == null) return new List<LocationSuggestion>();

                return results.Select(r => new LocationSuggestion
                {
                    Name = r.Name,
                    Country = r.Country,
                    State = r.State,
                    Latitude = r.Lat,
                    Longitude = r.Lon
                }).ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Geocoding API error: {ex.Message}");
                return new List<LocationSuggestion>();
            }
        }

        // Method to get popular country codes for autocomplete
        public static List<CountryCode> GetCountryCodes()
        {
            return new List<CountryCode>
            {
                new() { Code = "AD", Name = "Andorra" },
                new() { Code = "AE", Name = "United Arab Emirates" },
                new() { Code = "AF", Name = "Afghanistan" },
                new() { Code = "AG", Name = "Antigua and Barbuda" },
                new() { Code = "AI", Name = "Anguilla" },
                new() { Code = "AL", Name = "Albania" },
                new() { Code = "AM", Name = "Armenia" },
                new() { Code = "AO", Name = "Angola" },
                new() { Code = "AQ", Name = "Antarctica" },
                new() { Code = "AR", Name = "Argentina" },
                new() { Code = "AS", Name = "American Samoa" },
                new() { Code = "AT", Name = "Austria" },
                new() { Code = "AU", Name = "Australia" },
                new() { Code = "AW", Name = "Aruba" },
                new() { Code = "AX", Name = "Åland Islands" },
                new() { Code = "AZ", Name = "Azerbaijan" },
                new() { Code = "BA", Name = "Bosnia and Herzegovina" },
                new() { Code = "BB", Name = "Barbados" },
                new() { Code = "BD", Name = "Bangladesh" },
                new() { Code = "BE", Name = "Belgium" },
                new() { Code = "BF", Name = "Burkina Faso" },
                new() { Code = "BG", Name = "Bulgaria" },
                new() { Code = "BH", Name = "Bahrain" },
                new() { Code = "BI", Name = "Burundi" },
                new() { Code = "BJ", Name = "Benin" },
                new() { Code = "BL", Name = "Saint Barthélemy" },
                new() { Code = "BM", Name = "Bermuda" },
                new() { Code = "BN", Name = "Brunei" },
                new() { Code = "BO", Name = "Bolivia" },
                new() { Code = "BQ", Name = "Caribbean Netherlands" },
                new() { Code = "BR", Name = "Brazil" },
                new() { Code = "BS", Name = "Bahamas" },
                new() { Code = "BT", Name = "Bhutan" },
                new() { Code = "BV", Name = "Bouvet Island" },
                new() { Code = "BW", Name = "Botswana" },
                new() { Code = "BY", Name = "Belarus" },
                new() { Code = "BZ", Name = "Belize" },
                new() { Code = "CA", Name = "Canada" },
                new() { Code = "CC", Name = "Cocos Islands" },
                new() { Code = "CD", Name = "DR Congo" },
                new() { Code = "CF", Name = "Central African Republic" },
                new() { Code = "CG", Name = "Republic of the Congo" },
                new() { Code = "CH", Name = "Switzerland" },
                new() { Code = "CI", Name = "Côte d'Ivoire" },
                new() { Code = "CK", Name = "Cook Islands" },
                new() { Code = "CL", Name = "Chile" },
                new() { Code = "CM", Name = "Cameroon" },
                new() { Code = "CN", Name = "China" },
                new() { Code = "CO", Name = "Colombia" },
                new() { Code = "CR", Name = "Costa Rica" },
                new() { Code = "CU", Name = "Cuba" },
                new() { Code = "CV", Name = "Cape Verde" },
                new() { Code = "CW", Name = "Curaçao" },
                new() { Code = "CX", Name = "Christmas Island" },
                new() { Code = "CY", Name = "Cyprus" },
                new() { Code = "CZ", Name = "Czechia" },
                new() { Code = "DE", Name = "Germany" },
                new() { Code = "DJ", Name = "Djibouti" },
                new() { Code = "DK", Name = "Denmark" },
                new() { Code = "DM", Name = "Dominica" },
                new() { Code = "DO", Name = "Dominican Republic" },
                new() { Code = "DZ", Name = "Algeria" },
                new() { Code = "EC", Name = "Ecuador" },
                new() { Code = "EE", Name = "Estonia" },
                new() { Code = "EG", Name = "Egypt" },
                new() { Code = "EH", Name = "Western Sahara" },
                new() { Code = "ER", Name = "Eritrea" },
                new() { Code = "ES", Name = "Spain" },
                new() { Code = "ET", Name = "Ethiopia" },
                new() { Code = "FI", Name = "Finland" },
                new() { Code = "FJ", Name = "Fiji" },
                new() { Code = "FK", Name = "Falkland Islands" },
                new() { Code = "FM", Name = "Micronesia" },
                new() { Code = "FO", Name = "Faroe Islands" },
                new() { Code = "FR", Name = "France" },
                new() { Code = "GA", Name = "Gabon" },
                new() { Code = "GB", Name = "United Kingdom" },
                new() { Code = "GD", Name = "Grenada" },
                new() { Code = "GE", Name = "Georgia" },
                new() { Code = "GF", Name = "French Guiana" },
                new() { Code = "GG", Name = "Guernsey" },
                new() { Code = "GH", Name = "Ghana" },
                new() { Code = "GI", Name = "Gibraltar" },
                new() { Code = "GL", Name = "Greenland" },
                new() { Code = "GM", Name = "Gambia" },
                new() { Code = "GN", Name = "Guinea" },
                new() { Code = "GP", Name = "Guadeloupe" },
                new() { Code = "GQ", Name = "Equatorial Guinea" },
                new() { Code = "GR", Name = "Greece" },
                new() { Code = "GS", Name = "South Georgia" },
                new() { Code = "GT", Name = "Guatemala" },
                new() { Code = "GU", Name = "Guam" },
                new() { Code = "GW", Name = "Guinea-Bissau" },
                new() { Code = "GY", Name = "Guyana" },
                new() { Code = "HK", Name = "Hong Kong" },
                new() { Code = "HM", Name = "Heard Island" },
                new() { Code = "HN", Name = "Honduras" },
                new() { Code = "HR", Name = "Croatia" },
                new() { Code = "HT", Name = "Haiti" },
                new() { Code = "HU", Name = "Hungary" },
                new() { Code = "ID", Name = "Indonesia" },
                new() { Code = "IE", Name = "Ireland" },
                new() { Code = "IL", Name = "Israel" },
                new() { Code = "IM", Name = "Isle of Man" },
                new() { Code = "IN", Name = "India" },
                new() { Code = "IO", Name = "British Indian Ocean Territory" },
                new() { Code = "IQ", Name = "Iraq" },
                new() { Code = "IR", Name = "Iran" },
                new() { Code = "IS", Name = "Iceland" },
                new() { Code = "IT", Name = "Italy" },
                new() { Code = "JE", Name = "Jersey" },
                new() { Code = "JM", Name = "Jamaica" },
                new() { Code = "JO", Name = "Jordan" },
                new() { Code = "JP", Name = "Japan" },
                new() { Code = "KE", Name = "Kenya" },
                new() { Code = "KG", Name = "Kyrgyzstan" },
                new() { Code = "KH", Name = "Cambodia" },
                new() { Code = "KI", Name = "Kiribati" },
                new() { Code = "KM", Name = "Comoros" },
                new() { Code = "KN", Name = "Saint Kitts and Nevis" },
                new() { Code = "KP", Name = "North Korea" },
                new() { Code = "KR", Name = "South Korea" },
                new() { Code = "KW", Name = "Kuwait" },
                new() { Code = "KY", Name = "Cayman Islands" },
                new() { Code = "KZ", Name = "Kazakhstan" },
                new() { Code = "LA", Name = "Laos" },
                new() { Code = "LB", Name = "Lebanon" },
                new() { Code = "LC", Name = "Saint Lucia" },
                new() { Code = "LI", Name = "Liechtenstein" },
                new() { Code = "LK", Name = "Sri Lanka" },
                new() { Code = "LR", Name = "Liberia" },
                new() { Code = "LS", Name = "Lesotho" },
                new() { Code = "LT", Name = "Lithuania" },
                new() { Code = "LU", Name = "Luxembourg" },
                new() { Code = "LV", Name = "Latvia" },
                new() { Code = "LY", Name = "Libya" },
                new() { Code = "MA", Name = "Morocco" },
                new() { Code = "MC", Name = "Monaco" },
                new() { Code = "MD", Name = "Moldova" },
                new() { Code = "ME", Name = "Montenegro" },
                new() { Code = "MF", Name = "Saint Martin" },
                new() { Code = "MG", Name = "Madagascar" },
                new() { Code = "MH", Name = "Marshall Islands" },
                new() { Code = "MK", Name = "North Macedonia" },
                new() { Code = "ML", Name = "Mali" },
                new() { Code = "MM", Name = "Myanmar" },
                new() { Code = "MN", Name = "Mongolia" },
                new() { Code = "MO", Name = "Macao" },
                new() { Code = "MP", Name = "Northern Mariana Islands" },
                new() { Code = "MQ", Name = "Martinique" },
                new() { Code = "MR", Name = "Mauritania" },
                new() { Code = "MS", Name = "Montserrat" },
                new() { Code = "MT", Name = "Malta" },
                new() { Code = "MU", Name = "Mauritius" },
                new() { Code = "MV", Name = "Maldives" },
                new() { Code = "MW", Name = "Malawi" },
                new() { Code = "MX", Name = "Mexico" },
                new() { Code = "MY", Name = "Malaysia" },
                new() { Code = "MZ", Name = "Mozambique" },
                new() { Code = "NA", Name = "Namibia" },
                new() { Code = "NC", Name = "New Caledonia" },
                new() { Code = "NE", Name = "Niger" },
                new() { Code = "NF", Name = "Norfolk Island" },
                new() { Code = "NG", Name = "Nigeria" },
                new() { Code = "NI", Name = "Nicaragua" },
                new() { Code = "NL", Name = "Netherlands" },
                new() { Code = "NO", Name = "Norway" },
                new() { Code = "NP", Name = "Nepal" },
                new() { Code = "NR", Name = "Nauru" },
                new() { Code = "NU", Name = "Niue" },
                new() { Code = "NZ", Name = "New Zealand" },
                new() { Code = "OM", Name = "Oman" },
                new() { Code = "PA", Name = "Panama" },
                new() { Code = "PE", Name = "Peru" },
                new() { Code = "PF", Name = "French Polynesia" },
                new() { Code = "PG", Name = "Papua New Guinea" },
                new() { Code = "PH", Name = "Philippines" },
                new() { Code = "PK", Name = "Pakistan" },
                new() { Code = "PL", Name = "Poland" },
                new() { Code = "PM", Name = "Saint Pierre and Miquelon" },
                new() { Code = "PN", Name = "Pitcairn Islands" },
                new() { Code = "PR", Name = "Puerto Rico" },
                new() { Code = "PS", Name = "Palestine" },
                new() { Code = "PT", Name = "Portugal" },
                new() { Code = "PW", Name = "Palau" },
                new() { Code = "PY", Name = "Paraguay" },
                new() { Code = "QA", Name = "Qatar" },
                new() { Code = "RE", Name = "Réunion" },
                new() { Code = "RO", Name = "Romania" },
                new() { Code = "RS", Name = "Serbia" },
                new() { Code = "RU", Name = "Russia" },
                new() { Code = "RW", Name = "Rwanda" },
                new() { Code = "SA", Name = "Saudi Arabia" },
                new() { Code = "SB", Name = "Solomon Islands" },
                new() { Code = "SC", Name = "Seychelles" },
                new() { Code = "SD", Name = "Sudan" },
                new() { Code = "SE", Name = "Sweden" },
                new() { Code = "SG", Name = "Singapore" },
                new() { Code = "SH", Name = "Saint Helena" },
                new() { Code = "SI", Name = "Slovenia" },
                new() { Code = "SJ", Name = "Svalbard and Jan Mayen" },
                new() { Code = "SK", Name = "Slovakia" },
                new() { Code = "SL", Name = "Sierra Leone" },
                new() { Code = "SM", Name = "San Marino" },
                new() { Code = "SN", Name = "Senegal" },
                new() { Code = "SO", Name = "Somalia" },
                new() { Code = "SR", Name = "Suriname" },
                new() { Code = "SS", Name = "South Sudan" },
                new() { Code = "ST", Name = "São Tomé and Príncipe" },
                new() { Code = "SV", Name = "El Salvador" },
                new() { Code = "SX", Name = "Sint Maarten" },
                new() { Code = "SY", Name = "Syria" },
                new() { Code = "SZ", Name = "Eswatini" },
                new() { Code = "TC", Name = "Turks and Caicos Islands" },
                new() { Code = "TD", Name = "Chad" },
                new() { Code = "TF", Name = "French Southern Territories" },
                new() { Code = "TG", Name = "Togo" },
                new() { Code = "TH", Name = "Thailand" },
                new() { Code = "TJ", Name = "Tajikistan" },
                new() { Code = "TK", Name = "Tokelau" },
                new() { Code = "TL", Name = "Timor-Leste" },
                new() { Code = "TM", Name = "Turkmenistan" },
                new() { Code = "TN", Name = "Tunisia" },
                new() { Code = "TO", Name = "Tonga" },
                new() { Code = "TR", Name = "Turkey" },
                new() { Code = "TT", Name = "Trinidad and Tobago" },
                new() { Code = "TV", Name = "Tuvalu" },
                new() { Code = "TW", Name = "Taiwan" },
                new() { Code = "TZ", Name = "Tanzania" },
                new() { Code = "UA", Name = "Ukraine" },
                new() { Code = "UG", Name = "Uganda" },
                new() { Code = "UM", Name = "U.S. Minor Outlying Islands" },
                new() { Code = "US", Name = "United States" },
                new() { Code = "UY", Name = "Uruguay" },
                new() { Code = "UZ", Name = "Uzbekistan" },
                new() { Code = "VA", Name = "Vatican City" },
                new() { Code = "VC", Name = "Saint Vincent and the Grenadines" },
                new() { Code = "VE", Name = "Venezuela" },
                new() { Code = "VG", Name = "British Virgin Islands" },
                new() { Code = "VI", Name = "U.S. Virgin Islands" },
                new() { Code = "VN", Name = "Vietnam" },
                new() { Code = "VU", Name = "Vanuatu" },
                new() { Code = "WF", Name = "Wallis and Futuna" },
                new() { Code = "WS", Name = "Samoa" },
                new() { Code = "YE", Name = "Yemen" },
                new() { Code = "YT", Name = "Mayotte" },
                new() { Code = "ZA", Name = "South Africa" },
                new() { Code = "ZM", Name = "Zambia" },
                new() { Code = "ZW", Name = "Zimbabwe" }
            };
        }

        // Helper method to create WeatherData from API response
        private WeatherData CreateWeatherData(OpenWeatherResponse weather)
        {
            return new WeatherData
            {
                City = weather.Name,
                CountryCode = weather.Sys.Country,
                Datetime = DateTime.Now.ToString("dddd, d MMMM yyyy HH:mm", new CultureInfo("en-US")),
                Temperature = (int)Math.Round(weather.Main.Temp),
                FeelsLike = (int)Math.Round(weather.Main.FeelsLike),
                Description = weather.Weather[0].Description,
                WindDirection = ConvertWindDirection(weather.Wind.Deg),
                Humidity = weather.Main.Humidity,
                CloudCoverage = weather.Clouds.All,
                WindSpeed = (int)Math.Round(weather.Wind.Speed),
                Condition = MapCondition(weather.Weather[0].Main),
                timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond
            };
        }

        private string ConvertWindDirection(int deg)
        {
            if (deg >= 315 || deg < 45) return "N";
            if (deg >= 45 && deg < 135) return "E";
            if (deg >= 135 && deg < 225) return "S";
            return "W";
        }

        private WeatherCondition MapCondition(string main) => main.ToLower() switch
        {
            "clear" => WeatherCondition.Sunny,
            "clouds" => WeatherCondition.Cloudy,
            "rain" => WeatherCondition.Rainy,
            "drizzle" => WeatherCondition.Rainy,
            _ => WeatherCondition.PartlyCloudy
        };

        private class OpenWeatherResponse
        {
            public WeatherMain Main { get; set; } = null!;
            public List<WeatherDescription> Weather { get; set; } = null!;
            public WindInfo Wind { get; set; } = null!;
            public CloudsInfo Clouds { get; set; } = null!;
            public SysInfo Sys { get; set; } = null!;
            public string Name { get; set; } = string.Empty;

            public class WeatherMain
            {
                public double Temp { get; set; }            
                
                [JsonPropertyName("feels_like")]
                public double FeelsLike { get; set; }
                public int Humidity { get; set; }
            }

            public class WeatherDescription
            {
                public string Main { get; set; } = string.Empty;
                public string Description { get; set; } = string.Empty;
            }

            public class WindInfo
            {
                public double Speed { get; set; }
                public int Deg { get; set; }
            }

            public class CloudsInfo
            {
                public int All { get; set; }
            }
        }

        private class SysInfo
        {
            public string Country { get; set; } = string.Empty;
        }
    }
}
