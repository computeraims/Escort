using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Escort.Models
{
    public class HyperCommand
    {
        public string Name { get; set; }

        public string Usage { get; set; }

        public string Description { get; set; }

        public virtual void execute(CSteamID executor, string[] args) { }
    }
}
