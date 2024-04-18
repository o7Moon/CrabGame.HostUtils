using BepInEx;
using BepInEx.IL2CPP;
using BepInEx.Configuration;
using HarmonyLib;
using qol_core;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine;

namespace hostutils
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BasePlugin
    {
        public static Plugin Instance;
        public static Mod qolcoremod;

        public static int forcedNextMap = -1;
        public static int forcedNextMode = -1;

        public static Dictionary<string, int> mapnames;
        public static Dictionary<string, int> modenames;

        public static bool isHost() {
            return SteamManager.Instance.prop_CSteamID_0 == SteamManager.Instance.prop_CSteamID_1;
        }

        static ConfigEntry<bool> areSnowballsDisabled;
        static ConfigEntry<bool> skipWinScreen;
        public override void Load()
        {
            Instance = this;
            Harmony.CreateAndPatchAll(typeof(Plugin));

            SceneManager.sceneLoaded += (UnityAction<Scene,LoadSceneMode>) onSceneLoad;

            qolcoremod = Mods.RegisterMod(PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION, "Extra commands and options that make hosting lobbies nicer.","o7Moon/CrabGame.HostUtils");

            Commands.RegisterCommand("forcenextmap", "forcenextmap (name|id)", "force the game to pick a specific map next.", qolcoremod, ForceNextMapCommand);

            Commands.RegisterCommand("forcenextmode", "forcenextmode (name|id)", "force the game to pick a specific mode next.", qolcoremod, ForceNextModeCommand);

            Commands.RegisterCommand("vaporize", "vaporize (name|num|steamid)", "instantly vaporize a player.", qolcoremod, KillCommand);

            Commands.RegisterCommand("steamid", "steamid (name|num|steamid)", "show a player's steamid in chat", qolcoremod, SteamIDCommand);

            Commands.RegisterCommand("restart", "restart", "restarts the lobby, ending any current game.", qolcoremod, RestartCommand);

            Commands.RegisterCommand("rename", "rename (new name)", "changes the lobby name.", qolcoremod, RenameLobby);

            Commands.RegisterCommand("start", "start", "starts the game", qolcoremod, StartCommand);

            Commands.RegisterCommand("time", "time", "sets the round timer", qolcoremod, TimeCommand);

            areSnowballsDisabled = Config.Bind<bool>("Lobby Settings", "Disable Snowballs", false, "if true, dont allow snowballs to be picked up.");

            skipWinScreen = Config.Bind<bool>("Lobby Settings", "Skip Win Screen", false, "if true, skip the win screen.");

            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        public void onSceneLoad(Scene s, LoadSceneMode m){
            Config.Reload();
        }

        [HarmonyPatch(typeof(GameUI), nameof(GameUI.Update))]
        [HarmonyPostfix]
        public static void onGameUIUpdate(GameUI __instance) {
            if (Input.GetKeyDown(KeyCode.F5)) Instance.Config.Reload();
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
                modenames = new Dictionary<string, int>();
                foreach (GameModeData mode in GameModeManager.Instance.allGameModes) {
                    modenames.Add(mode.name.Replace(" ","").ToLower(), mode.id);
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

        public static bool ForceNextModeCommand(List<string> arguments) {
            if (!isHost()) return false;
            if (System.Int32.TryParse(arguments[1], out int modeID)) {
                forcedNextMode = modeID;
            } else {
                List<string> args = RemoveCommandFromArgs(arguments);
                string modename = args.Join(null, " ").ToLower();
                if (!modenames.ContainsKey(modename)) return false;
                forcedNextMode = modenames[modename];
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
            if (!isHost()) return false;
            List<string> args = RemoveCommandFromArgs(arguments);
            PlayerManager pm = GetPlayer(System.String.Join(" ", args), PlayerType.Both);
            if (pm == null) return false;

            qol_core.Plugin.SendMessage($"{pm.username}'s steamid is {pm.steamProfile}", qolcoremod);

            return true;
        }

        public static bool TimeCommand(List<string> arguments) {
            if (!isHost()) return false;
            List<string> args = RemoveCommandFromArgs(arguments);
            if (!float.TryParse(args[0], out float time)) return false;

            GameManager.Instance.gameMode.freezeTimer.field_Private_Single_0 = time;

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

        [HarmonyPatch(typeof(GameModeManager), nameof(GameModeManager.GetGameMode))]
        [HarmonyPrefix]
        public static void GetModeHook(GameModeManager __instance, ref int __0) {
            if (!isHost()) return;
            if (forcedNextMode > -1) {
                __0 = forcedNextMode;
                forcedNextMode = -1;
            }
        }
        [HarmonyPatch(typeof(GameServer), nameof(GameServer.ForceGiveWeapon))]
        [HarmonyPrefix]
        [HarmonyPriority(403)]
        public static bool onGiveWeapon(ulong __0, int __1, int __2) {
            if (!isHost() || !areSnowballsDisabled.Value || __1 != 9) return true;
            return false;
        }
        [HarmonyPatch(typeof(ServerSend), nameof(ServerSend.GameOver))]
        [HarmonyPrefix]
        public static bool onWinScreen(ulong __0) {
            if (!isHost() || !skipWinScreen.Value) return true;
            GameLoop.Instance.RestartLobby();
            return false;
        }
    }
}