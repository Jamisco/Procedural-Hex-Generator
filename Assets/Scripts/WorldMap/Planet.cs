﻿using Assets.Scripts.WorldMap.Biosphere;
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

        /// <summary>
        /// Max Sunlight in Fahrenheit divided by 100
        /// </summary>
        [Range(0, 13)]
        public float SunlightIntensity;

        public float SunlightArea;

        [Range(10, 365)]
        public int DaysInYear;

        public int currentDay;

        [SerializeField]
        public Terrestrial TerrestrialBody;

        [SerializeField]
        public Marine MarineBody;
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