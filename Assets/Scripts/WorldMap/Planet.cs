using Assets.Scripts.WorldMap.Biosphere;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI; 
using UnityEngine.XR;
using static Assets.Scripts.WorldMap.Biosphere.SurfaceBody;
using static Assets.Scripts.WorldMap.PlanetGenerator;

namespace Assets.Scripts.WorldMap
{
    [Serializable]
    
    // MonoBeavhiour provides a framework for creating custom behaviours and
    // interactivity in your Unity Game. By attaching scripts that inherit from
    // MonoBehaviour to the game ojbect.
    public class Planet : MonoBehaviour
    {
        public Vector2Int PlanetSize;
        public Vector2 InitialSunRayFocus;
        public Vector2Int SunlightVector;
        public float SunMovementAmplitude;
        public float SunMovementFrequency;
        public float SunlightIntensity;
        public float SunlightArea;
        public int DaysInYear;
        public int currentDay;
        public float baseTemperature = 25.0f;
        public float WeatherYPosition;

        public GameObject uiPanel;
        public Slider daysSlider;
        public Slider yPositionSlider;
        public Dropdown sunMovementDropdown;

        [SerializeField]
        public Terrestrial TerrestrialBody;

        [SerializeField]
        public Marine MarineBody;

        private SunMovementPattern currentSunMovementPattern;
        public enum SunMovementPattern
        {
            HorizontalStraightLine,
            VerticalStraightLine,
            SinCosineWave
        }

        public enum Season
        {
            Spring,
            Summer,
            Autumn,
            Winter
        }

        private void Start()
        {
            Initialize();
        }

        public Vector2Int SunMovementVector;
        
        [Range(0, 2)]
        public int SunMovementPatterns;
        private object currentSeason;
        private int seasonChangeThreshold;

        public void SetSunMovementPattern()
        {
            switch (SunMovementPatterns)
            {
                case (int) SunMovementPattern.HorizontalStraightLine:
                    SunMovementVector = new Vector2Int(1, 0);
                    break;
                case (int) SunMovementPattern.VerticalStraightLine:
                    SunMovementVector = new Vector2Int(0, 1);
                    break;
                case (int) SunMovementPattern.SinCosineWave:
                    int time = (int)Time.time; // Using Unity's Time
                    int XPosition = (int)(time * SunMovementFrequency); // frequency determins how fast the wave oscillates
                    int YPosition = (int)(Mathf.Sin(XPosition) * SunMovementAmplitude); // amplitude scales the wave vertically 
                    SunMovementVector = new Vector2Int(XPosition, YPosition);
                    break;
            }
        }

        public Vector2Int CurrentSunRayFocus
        {
            get
            {
                int day = currentDay % DaysInYear;

                int xSpeed = Mathf.CeilToInt( (float)PlanetSize.x / DaysInYear);
                int ySpeed = Mathf.CeilToInt((float)PlanetSize.y / DaysInYear);

                Vector2Int speed = new Vector2Int(xSpeed, ySpeed);

                //  day = (day == 0) ? 1 : day;

                Vector2Int currPos = day * SunlightVector * speed;

                // wrap it around so its doesnt exceed the map size
                int x = (int)((currPos.x + InitialSunRayFocus.x) % PlanetSize.x);
                int y = (int)((currPos.y + InitialSunRayFocus.y) % PlanetSize.y);

                return new Vector2Int(x, y);
            }
        }

        public BiomeProperties GetBiomeProperties(GridValues gridValues)
        {
            BiomeProperties props;
            
            if (gridValues.SurfaceType == SurfaceType.Terrestrial)
            {
                props = TerrestrialBody.GetBiomeProperties(gridValues);
            }
            else
            {
                props = MarineBody.GetBiomeProperties(gridValues);
            }

            return props;
        }

        public void Initialize()
        {
            TerrestrialBody.Init();
            MarineBody.Init();

            // Initialize UI elemets
            daysSlider.value = DaysInYear;
            yPositionSlider.value = WeatherYPosition;

            // Attach a method to be called when the sliders are adjusted 
            daysSlider.onValueChanged.AddListener(OnDaysSliderChanged);
            yPositionSlider.onValueChanged.AddListener(OnYPositionSliderChanged);
            sunMovementDropdown.onValueChanged.AddListener(OnSunMovementDropdownChanged);
        }

        private void OnDaysSliderChanged(float value)
        {
            // Update the number of days in a year
            DaysInYear = Mathf.RoundToInt(value);
        }

        private void OnYPositionSliderChanged(float value)
        {
            // Update the Y position for weather adjustments
            WeatherYPosition = value;
        }

        private void OnSunMovementDropdownChanged(int value)
        {
            // Update the sun movement pattern based on the dropdown selection
            currentSunMovementPattern = (SunMovementPattern)value;

            // Implement your sun movement logic based on the chosen pattern
        }

        private void Update()
        {
            SimulateSeasons();
            SimulateWeather();
        }

        private void SimulateSeasons()
        {   
            // seasons repeat in a cycle each year, using sine wave is a good choice
            // with smooth transition sine wave provies a smove transition between seasons.
            float seasonValue = Mathf.Sin(2 * Mathf.PI * currentDay / DaysInYear);

            // This uses contional statements that assigns a value to currentSeasons based on the value of seasonValue.
            // The following checks seasonValue against different thresholds.
            currentSeason = seasonValue < -seasonChangeThreshold
                ? Season.Winter
                : seasonValue < seasonChangeThreshold
                    ? Season.Spring
                    : seasonValue < 2 * seasonChangeThreshold
                        ? Season.Summer
                        : Season.Autumn;


            // Update game world based on the current season
            switch (currentSeason)
            {
                case Season.Winter:
                    // Adjust visual elements for winter, e.g., add snow to the terrain
                    break;
                case Season.Spring:
                    // Change terrain appearance for spring, e.g., remove snow
                    break;
                case Season.Summer:
                    // Alter world visuals for summer, e.g., vibrant foliage
                    break;
                case Season.Autumn:
                    // Adjust terrain for autumn, e.g., falling leaves
                    break;
            }
        }

        private void SimulateWeather()
        {
            // Simulate weather based on the Y position
            // You can use the WeatherYPosition parameter to adjust weather-related calculations
            // Update temperature, rainfall, and other weather parameters
            

            // Apply temperature to your game world
            // Example: Adjust the material color of a GameObject based on temperature
          
            // Consider other weather effects, such as adjusting rainfall, based on WeatherYPosition
        

            // Modify game world based on temperature and rainfall, e.g., affect plant growth or biome types
        }
        public enum SurfaceType
        {
            Terrestrial,
            Marine
        }

    }
}
