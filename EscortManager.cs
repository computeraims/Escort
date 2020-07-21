using Escort.Models;
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
        public HyperCommand EscortCommand;
        public static Dictionary<CSteamID, CSteamID> EscortingPlayers;
        public static Dictionary<CSteamID, CSteamID> EscortedPlayers;
        public HyperCommand LoadCommand;
        public HyperCommand UnloadCommand;

        public void Awake()
        {
            Console.WriteLine("EscortManager loaded");
            ChatManager.onChatted += OnChatted;
            PlayerLife.onPlayerDied += OnPlayerDied;
            Provider.onEnemyDisconnected += OnPlayerDisconnected;

            EscortingPlayers = new Dictionary<CSteamID, CSteamID>();
            EscortedPlayers = new Dictionary<CSteamID, CSteamID>();

            EscortCommand = new CommandEscort();
            LoadCommand = new CommandLoad();
            UnloadCommand = new CommandUnload();
        }

        private void OnChatted(SteamPlayer player, EChatMode Mode, ref Color Color, ref bool isRich, string text, ref bool isVisible)
        {
            if (text[0] == '/')
            {
                isVisible = false;
                string[] InputArray = text.Split(' ');
                InputArray[0] = InputArray[0].Substring(1);
                string[] args = InputArray.Skip(1).ToArray();

                if (InputArray[0].ToLower() == "escort")
                {
                    EscortCommand.execute(player.playerID.steamID, args);
                }

                if (InputArray[0].ToLower() == "load")
                {
                    LoadCommand.execute(player.playerID.steamID, args);
                }

                if (InputArray[0].ToLower() == "unload")
                {
                    UnloadCommand.execute(player.playerID.steamID, args);
                }
            }
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

        public class CommandEscort : HyperCommand
        {
            public CommandEscort()
            {
                Name = "escort";
                Description = "Escort a restrained player";
                Usage = "";
            }

            public override void execute(CSteamID executor, string[] args)
            {
                if (EscortingPlayers.ContainsKey(executor))
                {
                    CSteamID escorteeID;
                    if (EscortingPlayers.TryGetValue(executor, out escorteeID))
                    {
                        EscortedPlayers.Remove(escorteeID);
                        Player ply = PlayerTool.getPlayer(escorteeID);
                        ply.movement.sendPluginSpeedMultiplier(Main.Config.arrestMoveSpeed);
                    }

                    EscortingPlayers.Remove(executor);
                    return;
                } else if (EscortedPlayers.ContainsKey(executor))
                {
                    return;

                }

                SteamPlayer player = PlayerTool.getSteamPlayer(executor);
                RaycastInfo thingLocated = TraceRay(player, 20f, RayMasks.PLAYER | RayMasks.PLAYER_INTERACT);

                if (thingLocated.player != null && thingLocated.player.animator.gesture == EPlayerGesture.ARREST_START)
                {
                    EscortingPlayers.Add(player.playerID.steamID, thingLocated.player.channel.owner.playerID.steamID);
                    EscortedPlayers.Add(thingLocated.player.channel.owner.playerID.steamID, player.playerID.steamID);
                    thingLocated.player.movement.sendPluginSpeedMultiplier(0); // make it so players cant move while being escorted
                }
            }
        }

        public class CommandLoad : HyperCommand
        {
            public CommandLoad()
            {
                Name = "load";
                Description = "Load an escorted player into a vehicle";
                Usage = "";
            }
            public override void execute(CSteamID executor, string[] args)
            {
                SteamPlayer player = PlayerTool.getSteamPlayer(executor);
                RaycastInfo thingLocated = TraceRay(player, 20f, RayMasks.VEHICLE);

                if (thingLocated.vehicle != null)
                {
                    byte seat;
                    CSteamID escorteeID;

                    if (EscortingPlayers.TryGetValue(executor, out escorteeID))
                    {
                        Player ply = PlayerTool.getPlayer(escorteeID);


                        if (thingLocated.vehicle.tryAddPlayer(out seat, ply))
                        {
                            EscortedPlayers.Remove(escorteeID);
                            EscortingPlayers.Remove(executor);
                            VehicleManager.instance.channel.send("tellEnterVehicle", ESteamCall.ALL, ESteamPacket.UPDATE_RELIABLE_BUFFER, new object[] {
                                thingLocated.vehicle.instanceID,
                                seat,
                                escorteeID
                            });
                        }
                    }
                }
            }
        }

        public class CommandUnload : HyperCommand
        {
            public CommandUnload()
            {
                Name = "unload";
                Description = "Unload an escorted player from a vehicle";
                Usage = "";
            }

            public override void execute(CSteamID executor, string[] args)
            {
                SteamPlayer player = PlayerTool.getSteamPlayer(executor);
                RaycastInfo thingLocated = TraceRay(player, 20f, RayMasks.VEHICLE);

                if (thingLocated.vehicle != null && !thingLocated.vehicle.isLocked)
                {
                    for (int i = 0; i < thingLocated.vehicle.passengers.Length; i++)
                    {
                        Passenger passenger = thingLocated.vehicle.passengers[i];
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

        public static RaycastInfo TraceRay(SteamPlayer player, float distance, int masks)
        {
            return DamageTool.raycast(new Ray(player.player.look.aim.position, player.player.look.aim.forward), distance, masks);
        }
    }
}
