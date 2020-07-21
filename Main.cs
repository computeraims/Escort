using Escort.Utils;
using SDG.Framework.Modules;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Escort
{
    public class Main : MonoBehaviour, IModuleNexus
    {
        private static GameObject EscortObject;

        public static Main Instance;

        public static Config Config;

        public void initialize()
        {
            Instance = this;
            Console.WriteLine("Escort by Corbyn loaded");


            Patcher patch = new Patcher();
            Patcher.DoPatching();

            EscortObject = new GameObject("Escort");
            DontDestroyOnLoad(EscortObject);

            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            ConfigHelper.EnsureConfig($"{path}{Path.DirectorySeparatorChar}config.json");

            Config = ConfigHelper.ReadConfig($"{path}{Path.DirectorySeparatorChar}config.json");

            EscortObject.AddComponent<EscortManager>();
        }


        public void shutdown()
        {
            Instance = null;
        }
    }
}
