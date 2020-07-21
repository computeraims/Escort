using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Escort.Utils
{
    public class Patcher
    {
        public static void DoPatching()
        {
            var harmony = new Harmony("com.example.patch");

            harmony.PatchAll();
        }
    }
}
