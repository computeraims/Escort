using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace Escort.Utils
{
    public class Config
    {
        public float arrestMoveSpeed { get; set; }
        public bool allowEnterArrest { get; set; }

        public bool allowExitArrest { get; set; }
    }

    public class ConfigHelper
    {
        public static void EnsureConfig(string path)
        {
            if (!File.Exists(path))
            {
                Console.WriteLine("No config.json");

                JObject escortConfig = new JObject();
                escortConfig.Add("arrestMoveSpeed", 1);
                escortConfig.Add("allowEnterArrest", true);
                escortConfig.Add("allowExitArrest", true);

                using (StreamWriter file = File.CreateText(path))
                using (JsonTextWriter writer = new JsonTextWriter(file))
                {
                    escortConfig.WriteTo(writer);
                    Console.WriteLine("Generated Escort config");
                }
            }
        }

        public static Config ReadConfig(string path)
        {
            using (StreamReader file = File.OpenText(path))
            using (JsonTextReader reader = new JsonTextReader(file))
            {
                return JsonConvert.DeserializeObject<Config>(JToken.ReadFrom(reader).ToString());
            }
        }
    }
}
