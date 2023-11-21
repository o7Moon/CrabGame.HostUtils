using BepInEx;
using BepInEx.IL2CPP;
using HarmonyLib;
using qol_core;
using System.Collections.Generic;

namespace hostutils
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public static Mod qolcoremod;

        public static int forcedNextMap = -1;

        public static Dictionary<string, int> mapnames;

        public static bool isHost() {
            return SteamManager.Instance.prop_CSteamID_0 == SteamManager.Instance.prop_CSteamID_1;
        }
        public override void Load()
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));

            qolcoremod = Mods.RegisterMod(PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION, "Extra commands and options that make hosting lobbies nicer.");

            Commands.RegisterCommand("forcenextmap", "forcenextmap (name|id)", "force the game to pick a specific map next.", qolcoremod, ForceNextMapCommand);

            Commands.RegisterCommand("vaporize", "vaporize (name|num|steamid)", "instantly vaporize any player.", qolcoremod, KillCommand);

            Commands.RegisterCommand("steamid", "steamid (name|num|steamid)", "show a player's steamid in chat", qolcoremod, SteamIDCommand);

            Commands.RegisterCommand("restart", "restart", "restarts the lobby, ending any current game.", qolcoremod, RestartCommand);

            Commands.RegisterCommand("rename", "rename (new name)", "changes the lobby name.", qolcoremod, RenameLobby);

            Commands.RegisterCommand("start", "start", "starts the game", qolcoremod, StartCommand);

            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        [HarmonyPatch(typeof(SteamworksNative.SteamMatchmaking), nameof(SteamworksNative.SteamMatchmaking.SetLobbyData))]
        [HarmonyPostfix]
        public static void onLobbyDataSet(SteamworksNative.CSteamID __0, string __1, string __2){
            if (!isHost()) return;
            if (__1 == "Version"){// this gets set right when the lobby is made
                mapnames = new Dictionary<string, int>();
                foreach (Map map in MapManager.Instance.maps) {
                    mapnames.Add(map.name.Replace(" ","").ToLower(), map.id);
                }
            }
        }

        public static bool ForceNextMapCommand(List<string> arguments) {
            if (!isHost()) return false;
            if (System.Int32.TryParse(arguments[1], out int mapID)) {
                forcedNextMap = mapID;
            } else {
                List<string> args = RemoveCommandFromArgs(arguments);
                string mapname = args.Join(null, " ").ToLower();
                if (!mapnames.ContainsKey(mapname)) return false;
                forcedNextMap = mapnames[mapname];
            }
            return true;
        }

        public enum PlayerType {
            Active,
            Spectator,
            Both,
        }

        public static PlayerManager GetPlayer(ulong steamID, PlayerType ptype) {
            if ((ptype is PlayerType.Active or PlayerType.Both) && GameManager.Instance.activePlayers.ContainsKey(steamID)) {
                return GameManager.Instance.activePlayers[steamID];
            }
            if ((ptype is PlayerType.Spectator or PlayerType.Both) && GameManager.Instance.spectators.ContainsKey(steamID)) {
                return GameManager.Instance.spectators[steamID];
            }
            return null;
        }

        public static PlayerManager GetPlayer(string identifier, PlayerType ptype) {
            ulong steamID;
            if (System.Int32.TryParse(identifier, out int num)) {
                num--;
                var uidtosteamid = LobbyManager.Instance.field_Private_ArrayOf_UInt64_0;
                if (num >= 0 && num < uidtosteamid.Length) {
                    steamID = uidtosteamid[num];
                    PlayerManager pm = GetPlayer(steamID, ptype);
                    if (pm != null) return pm;
                }
            }
            if (System.UInt64.TryParse(identifier, out steamID)) {
                PlayerManager pm = GetPlayer(steamID, ptype);
                if (pm != null) return pm;
            }
            if (ptype is PlayerType.Active or PlayerType.Both) {
                foreach (Il2CppSystem.Collections.Generic.KeyValuePair<ulong, PlayerManager> pair in GameManager.Instance.activePlayers) {
                    PlayerManager pm = pair.Value;
                    if (pm.username.ToLower().Equals(identifier.ToLower())) return pm;
                }
            }
            if (ptype is PlayerType.Spectator or PlayerType.Both) {
                foreach (Il2CppSystem.Collections.Generic.KeyValuePair<ulong, PlayerManager> pair in GameManager.Instance.spectators) {
                    PlayerManager pm = pair.Value;
                    if (pm.username.ToLower().Equals(identifier.ToLower())) return pm;
                }
            }
            return null;
        }

        public static bool KillCommand(List<string> arguments) {
            if (!isHost()) return false;
            List<string> args = RemoveCommandFromArgs(arguments);
            PlayerManager pm = GetPlayer(System.String.Join(" ", args), PlayerType.Active);
            if (pm == null) return false;
            ServerSend.PlayerDied((ulong)pm.steamProfile, 1, UnityEngine.Vector3.zero);
            return true;
        }

        public static bool SteamIDCommand(List<string> arguments) {
            List<string> args = RemoveCommandFromArgs(arguments);
            PlayerManager pm = GetPlayer(System.String.Join(" ", args), PlayerType.Both);
            if (pm == null) return false;

            qol_core.Plugin.SendMessage($"{pm.username}'s steamid is {pm.steamProfile}", qolcoremod);

            return true;
        }

        public static bool RestartCommand(List<string> arguments) {
            if (!isHost()) return false;
            GameLoop.Instance.RestartLobby();
            return true;
        }

        public static bool RenameLobby(List<string> arguments) {
            if (!isHost()) return false;
            string newname = System.String.Join(" ", RemoveCommandFromArgs(arguments));
            SteamworksNative.SteamMatchmaking.SetLobbyData(SteamManager.Instance.currentLobby, "LobbyName", newname);
            return true;
        }

        public static bool StartCommand(List<string> arguments) {
            if (!isHost()) return false;
            GameLoop.Instance.StartGames();
            return true;
        }

        public static List<string> RemoveCommandFromArgs(List<string> arguments) {
            return arguments.GetRange(1,arguments.Count - 1);
        }

        [HarmonyPatch(typeof(MapManager), nameof(MapManager.GetMap))]
        [HarmonyPrefix]
        public static void GetMapHook(MapManager __instance, ref int __0) {
            if (!isHost()) return;
            if (forcedNextMap > -1) {
                __0 = forcedNextMap;
            }
        }

        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.LoadMap), new System.Type[] {typeof(int), typeof(int)})]
        [HarmonyPrefix]
        public static void LoadMapHook(ref int __0, int __1) {
            if (!isHost()) return;
            if (forcedNextMap > -1) {
                __0 = forcedNextMap;
                forcedNextMap = -1;
            }
        }
    }
}