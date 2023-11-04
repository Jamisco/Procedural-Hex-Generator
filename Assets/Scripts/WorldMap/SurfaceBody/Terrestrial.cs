using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Assets.Scripts.WorldMap.PlanetGenerator;

namespace Assets.Scripts.WorldMap.Biosphere
{
    [System.Serializable]
    public class Terrestrial : SurfaceBody
    {
        
        [SerializeField] private List<BiomeData> biomeDataList = new List<BiomeData>();
        private static Dictionary<int, BiomeProperties> biomePropertiesDict = new Dictionary<int, BiomeProperties>();
        public Terrestrial()
        {

        }

        private static readonly int[,] BiomeTable = new int[10, 10]
        {
            #region Explanation
            // X axis = temperature from cold to hot
            // Y axis = precipitation from dry to wet


            // each number represents the int conversion of the Biome

            // min temperature = -40f
            // max temperature = 140f

            // equation to get temperature = -40 + 18x, with X being the index

            // Rainfall is from 0 - 500cm 
            // equation to get rainfall = 50x, with X being the index


            // Temp:  -40, -22, -4, 14, 32, 50, 68, 86, 104, and 122. Fahrenheit

            // -40, -30, -20, -10, 0, 10, 20, 30, 40, 50, Celcius
            // Rain: 0, 50, 100, 150, 200, 250, 300, 350, 400, and 450.
            #endregion

            // PLease make sure your coordinates are in Array Indexes and not a 2D graph

            {9, 9, 9, 0, 1, 2, 3, 3, 3, 3 }, // 450
            {9, 9, 9, 0, 1, 2, 3, 3, 3, 3 }, // 400
            {9, 9, 9, 0, 1, 2, 2, 3, 3, 3 }, // 350
            {9, 9, 9, 0, 1, 2, 2, 3, 3, 3 }, // 300
            {9, 9, 9, 0, 1, 2, 2, 8, 8, 3 }, // 250
            {9, 9, 9, 0, 6, 6, 6, 8, 8, 8 }, // 200
            {9, 9, 9, 0, 1, 6, 6, 8, 8, 8 }, // 150
            {9, 9, 10, 0, 1, 6, 5, 8, 8, 8 }, // 100
            {10, 10, 9, 0, 0, 5, 5, 5, 7, 7 }, // 50
            {10, 10, 10, 0, 4, 4, 7, 7, 7, 7 }, // 0
        };

        public Biomes GetBiome(float temperature, float precipitation)
        {
            #region
            // The Biome table is structured in a 2d graph format, where the X axis is the temperature and the Y axis is the precipitation
            // so we have to convert a 2d graph coordinate to array index

            // think of temp and precipitation as percentages

            // Example, if temp = 0.5f, then it means the temperature is at the Math.floor(.5 * 9) = 4. Where 9 in the max index of X axis of our array

            // Example, if precipitation = 0.3f,  then it means the precipitation is at the Math.floor(.3 * 9) = 2. Where 9 in the max index of Y axis of our array

            // so (.5, .3) = (.5 * 9, .3 * 9) = (4.5, 2.7) = (4, 3) = in 2D graph coords -  = (6, 4) in our array coords/index

            // (.99, 1) = (8.91, 9) = (9, 9) 2d coords = (0, 9) in our array coords/index

            // (.68, .85) = (6.12, 7.65) = (6, 8) 2d coords = (1, 6) in our array coords/index

            // This gives us the formula to get array index
            // X = 9 - Math.Round(precipitation * 9)
            // Y = Math.Round(temperature * 9)

            // 2d coor (X, Y) = array indexes (9 - Y, X)

            // First we have to convert these numbers from 2d coordinates to array coords
            #endregion

            int x = (int)(9 - Math.Round(precipitation * 9));
            int y = (int)Math.Round(temperature * 9);

  
            return (Biomes)BiomeTable[x, y];
        }

        public override SurfaceBody.BiomeData GetBiomeData(GridValues grid)
        {

            float temp = grid.Temperature;
            float precip = grid.Precipitation;

            Biomes biomeEnum = GetBiome(temp, precip);
          
            // Using BiomePropertiesManager to get the properties.
            if (BiomePropertiesManager.TryGetValue(GetHash(biomeEnum), out BiomeProperties biomeProps))
            {
                if (biomeProps != null && biomeProps.Color != null && biomeProps.Texture != null)
                {
                    // If biomeProps is not null and contains valid data, proceed to create BiomeData
                    return new SurfaceBody.BiomeData(biomeEnum, biomeProps.Color, biomeProps.Texture);
                }
            }

            // If biomeProps is null or does not contain valid data, log an error and return default BiomeData
            Debug.LogError($"Biome properties for {biomeEnum} could not be found or are invalid.");
            return default; // Or provide a fallback/default BiomeData
        }

        public BiomeProperties GetBiomeProperties(Biomes biome)
        {
            int hash = GetHash(biome);

            if (biomePropertiesDict.TryGetValue(hash, out BiomeProperties props))
            {
                return props;
            }

            return default;
        }

        public void Init()
        {
            ConvertData();
        }


        [SerializeField] private List<BiomeData> biomeData = new List<BiomeData>();
        private void ConvertData()
        {
            biomePropertiesDict.Clear();
            
            foreach (BiomeData item in biomeData)
            {
                int hash = GetHash(item.biome);
                BiomeProperties props = new BiomeProperties(hash, item.color, item.texture);
                BiomePropertiesManager.Add(hash, props);
            }
        }

        private int GetHash(Biomes biome)
        {
            string name = nameof(Terrestrial) + biome.ToString();

            return name.GetHashCode();
        }
        
        [System.Serializable]
        public struct BiomeData
        {
            public Biomes biome;
            public Color color;
            public Texture2D texture;
           
            public BiomeData(Biomes biome, Color color, Texture2D texture)
            {
                this.biome = biome;
                this.color = color;
                this.texture = texture;
            }

            public override bool Equals(object obj)
            {
                return base.Equals(obj);
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }

            public override string ToString()
            {
                return base.ToString();
            }
        }
    }

    public class BiomeProperties
    {
        public int Hash { get; private set; }
        public Color Color { get; private set; }
        public Texture2D Texture { get; private set; }

        public BiomeProperties(int hash, Color color, Texture2D texture)
        {
            Hash = hash;
            Color = color;
            Texture = texture;
        }
    }
    internal static class BiomePropertiesManager
    {
        private static Dictionary<int, BiomeProperties> biomePropertiesDict = new Dictionary<int, BiomeProperties>();

        internal static void Clear()
        {
            biomePropertiesDict.Clear();
        }

        internal static bool TryGetValue(int hash, out BiomeProperties props)
        {
            return biomePropertiesDict.TryGetValue(hash, out props);
        }

        internal static void Add(int hash, BiomeProperties props)
        {
            biomePropertiesDict[hash] = props;
        }
    }
}
