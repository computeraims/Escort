using HarmonyLib;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Escort
{
    class EscortManager : MonoBehaviour
    {
        public static Dictionary<CSteamID, CSteamID> EscortingPlayers;
        public static Dictionary<CSteamID, CSteamID> EscortedPlayers;

        public void Awake()
        {
            Console.WriteLine("EscortManager loaded");

            PlayerLife.onPlayerDied += OnPlayerDied;
            Provider.onEnemyDisconnected += OnPlayerDisconnected;

            EscortingPlayers = new Dictionary<CSteamID, CSteamID>();
            EscortedPlayers = new Dictionary<CSteamID, CSteamID>();

            Commander.register(new CommandEscort());
            Commander.register(new CommandLoad());
            Commander.register(new CommandUnload());

        }

        private void OnPlayerDied(PlayerLife sender, EDeathCause cause, ELimb limb, CSteamID instigator)
        {
            if (EscortingPlayers.ContainsKey(sender.player.channel.owner.playerID.steamID))
            {
                CSteamID escorteeID;
                if (!EscortingPlayers.TryGetValue(sender.player.channel.owner.playerID.steamID, out escorteeID))
                {
                    return;
                }

                EscortingPlayers.Remove(sender.player.channel.owner.playerID.steamID);
                EscortedPlayers.Remove(escorteeID);
            } else if (EscortedPlayers.ContainsKey(sender.player.channel.owner.playerID.steamID))
            {
                CSteamID escorterID;
                if (!EscortedPlayers.TryGetValue(sender.player.channel.owner.playerID.steamID, out escorterID))
                {
                    return;
                }

                EscortedPlayers.Remove(sender.player.channel.owner.playerID.steamID);
                EscortingPlayers.Remove(escorterID);
            }
        }

        private void OnPlayerDisconnected(SteamPlayer player)
        {
            if (EscortingPlayers.ContainsKey(player.playerID.steamID))
            {
                CSteamID escorteeID;
                EscortingPlayers.TryGetValue(player.playerID.steamID, out escorteeID);
                if (!EscortingPlayers.TryGetValue(player.playerID.steamID, out escorteeID))
                {
                    return;
                }

                EscortingPlayers.Remove(player.playerID.steamID);
                EscortedPlayers.Remove(escorteeID);
                Player ply = PlayerTool.getPlayer(escorteeID);
            }
            else if (EscortedPlayers.ContainsKey(player.playerID.steamID))
            {
                CSteamID escorterID;
                if (!EscortedPlayers.TryGetValue(player.playerID.steamID, out escorterID))
                {
                    return;
                }

                EscortedPlayers.Remove(player.playerID.steamID);
                EscortingPlayers.Remove(escorterID);
            }
        }

        [HarmonyPatch(typeof(VehicleManager))]
        [HarmonyPatch("askEnterVehicle")]
        class AskEnterVehiclePatch
        {
            static bool Prefix(CSteamID steamID, uint instanceID, byte[] hash, byte engine)
            {
                Player ply = PlayerTool.getPlayer(steamID);
                if (ply.animator.gesture == EPlayerGesture.ARREST_START && !Main.Config.allowEnterArrest)
                {
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(VehicleManager))]
        [HarmonyPatch("askExitVehicle")]
        class AskExitVehiclePatch
        {
            static bool Prefix(CSteamID steamID, Vector3 velocity)
            {
                Player ply = PlayerTool.getPlayer(steamID);
                if (ply.animator.gesture == EPlayerGesture.ARREST_START && !Main.Config.allowExitArrest)
                {
                    return false;
                }

                return true;
            }
        }

        public void FixedUpdate()
        {
            foreach (KeyValuePair<CSteamID, CSteamID> entry in EscortedPlayers)
            {
                Player escorter = PlayerTool.getPlayer(entry.Value);
                Player escortee = PlayerTool.getPlayer(entry.Key);
                escortee.transform.position = escorter.transform.position;
            }

            foreach (SteamPlayer player in Provider.clients)
            {
                if (player.player.animator.gesture == EPlayerGesture.ARREST_START && player.player.movement.pluginSpeedMultiplier != Main.Config.arrestMoveSpeed)
                {
                    player.player.movement.sendPluginSpeedMultiplier(Main.Config.arrestMoveSpeed);
                } 
            }
        }

        public class CommandEscort : Command
        {
            protected override void execute(CSteamID executorID, string parameter)
            {
                if (EscortingPlayers.ContainsKey(executorID))
                {
                    CSteamID escorteeID;
                    if (EscortingPlayers.TryGetValue(executorID, out escorteeID))
                    {
                        EscortedPlayers.Remove(escorteeID);
                        Player ply = PlayerTool.getPlayer(escorteeID);
                        ply.movement.sendPluginSpeedMultiplier(Main.Config.arrestMoveSpeed);
                    }

                    EscortingPlayers.Remove(executorID);
                    return;
                }
                else if (EscortedPlayers.ContainsKey(executorID))
                {
                    return;

                }

                SteamPlayer player = PlayerTool.getSteamPlayer(executorID);

                List<Player> nearbyPlayers = new List<Player>();

                PlayerTool.getPlayersInRadius(player.player.movement.transform.position, 10f, nearbyPlayers);

                if (nearbyPlayers == null)
                {
                    return;
                }

                foreach (Player p in nearbyPlayers)
                {
                    if (p.animator.gesture == EPlayerGesture.ARREST_START)
                    {
                        EscortingPlayers.Add(player.playerID.steamID, p.channel.owner.playerID.steamID);
                        EscortedPlayers.Add(p.channel.owner.playerID.steamID, player.playerID.steamID);
                        p.movement.sendPluginSpeedMultiplier(0); // make it so players cant move while being escorted
                        return;
                    }
                }
            }

            public CommandEscort()
            {
                this.localization = new Local();
                this._command = "escort";
                this._info = "escort";
                this._help = "Escort handcuffed players";
            }
        }

        public class CommandLoad : Command
        {
            protected override void execute(CSteamID executorID, string parameter)
            {
                SteamPlayer player = PlayerTool.getSteamPlayer(executorID);

                List<InteractableVehicle> nearbyVehicles = new List<InteractableVehicle>();

                VehicleManager.getVehiclesInRadius(player.player.movement.transform.position, 15f, nearbyVehicles);

                foreach (InteractableVehicle v in nearbyVehicles)
                {
                    byte seat;
                    CSteamID escorteeID;

                    if (EscortingPlayers.TryGetValue(executorID, out escorteeID))
                    {
                        Player ply = PlayerTool.getPlayer(escorteeID);


                        if (v.tryAddPlayer(out seat, ply) && !v.isLocked)
                        {
                            EscortedPlayers.Remove(escorteeID);
                            EscortingPlayers.Remove(executorID);
                            VehicleManager.instance.channel.send("tellEnterVehicle", ESteamCall.ALL, ESteamPacket.UPDATE_RELIABLE_BUFFER, new object[] {
                                v.instanceID,
                                seat,
                                escorteeID
                            });
                        }
                    }
                }
            }

            public CommandLoad()
            {
                this.localization = new Local();
                this._command = "load";
                this._info = "load";
                this._help = "Load an escorting player into a vehicle";
            }
        }

        public class CommandUnload : Command
        {
            protected override void execute(CSteamID executorID, string parameter)
            {
                SteamPlayer player = PlayerTool.getSteamPlayer(executorID);

                List<InteractableVehicle> nearbyVehicles = new List<InteractableVehicle>();

                VehicleManager.getVehiclesInRadius(player.player.movement.transform.position, 15f, nearbyVehicles);

                foreach (InteractableVehicle v in nearbyVehicles)
                {
                    if (!v.isLocked)
                    {
                        for (int i = 0; i < v.passengers.Length; i++)
                        {
                            Passenger passenger = v.passengers[i];
                            if (passenger != null)
                            {
                                SteamPlayer ply = passenger.player;
                                if (ply != null)
                                {
                                    Player player2 = ply.player;
                                    if (!(player2 == null) && !player2.life.isDead)
                                    {
                                        if (ply.player.animator.gesture == EPlayerGesture.ARREST_START)
                                        {
                                            VehicleManager.forceRemovePlayer(ply.playerID.steamID);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            public CommandUnload()
            {
                this.localization = new Local();
                this._command = "unload";
                this._info = "unload";
                this._help = "Unload a restrained player from a vehicle";
            }
        }
    }
}
