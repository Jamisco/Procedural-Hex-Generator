using Assets.Scripts.WorldMap.Biosphere;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using static Assets.Scripts.WorldMap.Biosphere.SurfaceBody;
using static Assets.Scripts.WorldMap.PlanetGenerator;

namespace Assets.Scripts.WorldMap
{
    [Serializable]
    public class Planet
    {
        public Vector2Int PlanetSize; 

        /// <summary>
        /// The position at which the sun ray will be most intense
        /// </summary>
        public Vector2 InitialSunRayFocus;

        public Vector2Int SunlightVector;

        // User-Determined Adjustments for SunMovement for Sin/Cosine:
        [Range(0, 1)]
        public float SunMovementAmplitude;

        [Range(0, 1)]
        public float SunMovementFrequency;

        /// <summary>
        /// Max Sunlight in Fahrenheit divided by 100
        /// </summary>
        [Range(0, 13)]
        public float SunlightIntensity;

        public float SunlightArea;

        // in unity looking at the HexMap inspector you will see a sliding bar that will allow you to adjust the days in year range 10-365
        [Range(10, 365)]
        public int DaysInYear;

        public int currentDay;

        // this is the adjusted Y equation that will cause the weather to adjust accordingly
        [Range(0, 1)]
        public float WeatherYPosition;

        [SerializeField]
        public Terrestrial TerrestrialBody;

        [SerializeField]
        public Marine MarineBody;

        public enum SunMovementPattern
        {
            HorizontalStraightLine,
            VerticalStraightLine,
            SinCosineWave
        }

        public Vector2 SunMovementVector;
        
        [Range(0, 2)]
        public int SunMovementPatterns;
        public void SetSunMovementPattern()
        {
            switch (SunMovementPatterns)
            {
                case (int) SunMovementPattern.HorizontalStraightLine:
                    SunMovementVector = new Vector2(1, 0);
                    break;
                case (int) SunMovementPattern.VerticalStraightLine:
                    SunMovementVector = new Vector2(0, 1);
                    break;
                case (int) SunMovementPattern.SinCosineWave:
                    float time = Time.time; // Using Unity's Time
                    float XPosition = time * SunMovementFrequency; // frequency determins how fast the wave oscillates
                    float YPosition = Mathf.Sin(XPosition) * SunMovementAmplitude; // amplitude scales the wave vertically 
                    SunMovementVector = new Vector2(XPosition, YPosition);
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
        }
        
        public enum SurfaceType
        {
            Terrestrial,
            Marine
        }

    }
}
