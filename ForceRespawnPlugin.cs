// TODO: Gamepad/keyboard support
// TODO: 72h/168h marks on slider
// Todo: Nicer 3day/7day skyline
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections;
using System.Linq;
using UnityEngine;
using System.ComponentModel;
using System.Reflection;
using System;
using NodeCanvas.DialogueTrees;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ParadoxNotion;


namespace ForceRespawn
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class ForceRespawnPlugin : BaseUnityPlugin
    {
        public const string NAME = "ForceRespawn";
        public const string VERSION = "1.0.0";
        public const string CREATOR = "Jdbye";
        public const string GUID = CREATOR + NAME;

        internal static ManualLogSource Log;

        public static ConfigEntry<SpawnSelectionTypes> SpawnType;
        public static ConfigEntry<bool> EnableInventoryStackBugFix;

        internal static ForceRespawnPlugin Instance;

        internal void Awake()
        {
            Log = this.Logger;
            Log.LogInfo($"{NAME} {VERSION} by {CREATOR} loaded!");

            Instance = this;

            SpawnType = Config.Bind("Force Respawn", "Spawn Point", SpawnSelectionTypes.SamePlace, "Select the spawn type to be used");
            EnableInventoryStackBugFix = Config.Bind("Bug Fixes", "Inventory Stack Bug Fix", false, "Fixes the rare bug where your stacks are reduced to 1 when joining a multiplayer session by forcing a respawn");

            new Harmony(GUID).PatchAll();
        }

        internal void Update()
        {

        }

        public enum SpawnSelectionTypes
        {
            [Description("Closest")]
            Closest,
            [Description("Random")]
            Random,
            [Description("Last Used")]
            LastUsedSpawn,
            [Description("Current Position")]
            SamePlace
        }

        internal static Dictionary<string, Vector3> playerPositions = new Dictionary<string, Vector3>();
        internal static Dictionary<string, Quaternion> playerRotations = new Dictionary<string, Quaternion>();

        internal static int GetClosestSpawn()
        {
            int returnedSpawnIndex = -1;

            Vector3 hostPosition = CharacterManager.Instance.GetWorldHostCharacter().transform.position;
            float minDistance = float.MaxValue;
            for (int i = 0; i < SpawnPointManager.Instance.SpawnPoints.Count(); i++)
            {
                float dist = Vector3.Distance(SpawnPointManager.Instance.SpawnPoints[i].transform.position, hostPosition);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    returnedSpawnIndex = i;
                }
            }

            return returnedSpawnIndex;
        }

        internal static int GetSpawnIndex(SpawnSelectionTypes spawnType)
        {
            int returnedSpawnIndex;

            if (spawnType is SpawnSelectionTypes.Closest or SpawnSelectionTypes.SamePlace)
            {
                returnedSpawnIndex = GetClosestSpawn();
            }
            else if (spawnType == SpawnSelectionTypes.LastUsedSpawn)
            {
                returnedSpawnIndex = SpawnPointManager.Instance.LastSpawnPointUsed;
            }
            else
            {
                returnedSpawnIndex = UnityEngine.Random.Range(0, SpawnPointManager.Instance.SpawnPoints.Count());
            }

            return returnedSpawnIndex;
        }

        internal static IEnumerator ReloadMap(SpawnSelectionTypes? spawnType = null)
        {
            if (spawnType == null) spawnType = SpawnType.Value;
#if DEBUG
            Log.LogInfo($"Forcing respawn ({spawnType.GetDescription()}) - {AreaManager.Instance.CurrentArea.SceneName} - {AreaManager.Instance.CurrentArea.MapResourcesName}");
#endif
            // Get the spawn point index for the selected spawn type
            int spawnIndex = GetSpawnIndex((SpawnSelectionTypes)spawnType);
            // Get the current scene name, we will use this later
            string sceneName = AreaManager.Instance.CurrentArea.SceneName;
            // Store current player positions and rotations if needed
            if (spawnType == SpawnSelectionTypes.SamePlace) StorePlayerPositions();
            // Now reload the level
            NetworkLevelLoader.Instance.RequestReloadLevel(spawnIndex);
            if (spawnType != SpawnSelectionTypes.SamePlace) // If spawn type is not same location, we are done
                yield break;
            else
            {
                // We now need to teleport players back to their previous positions
                // Wait for map to load first
                var e = WaitForMapToLoad();
                while (e.MoveNext())
                    yield return e.Current;

                // Make sure we have not changed map for unknown reasons or this would break badly
                if (AreaManager.Instance.CurrentArea.SceneName == sceneName)
                {
                    // Teleport to previous position
                    //TeleportPlayers();
                    NetworkLevelLoader.Instance.RequestReloadLevel(spawnIndex);
                }
                yield break;
            }
        }

        internal static IEnumerator ReloadMapAfterJoin()
        {
            // Wait for room join to start
            var e = WaitForJoin();
            while (e.MoveNext())
                yield return e.Current;

            ForceRespawnPlugin.Log.LogInfo("Joined multiplayer session, performing stack bug fix...");

            // Reload the map
            var f = ReloadMap(ForceRespawnPlugin.SpawnSelectionTypes.SamePlace);
            while (f.MoveNext())
                yield return f.Current;
        }

        internal static IEnumerator WaitForMapToLoad()
        {
            // Wait for map to start loading
            while (!NetworkLevelLoader.Instance.IsSceneLoading)
            {
                yield return null;
            }

            // Wait for map to finish loading
            while (NetworkLevelLoader.Instance.InLoading || !NetworkLevelLoader.Instance.AllPlayerDoneLoading)
            {
                yield return null;
            }
        }

        internal static IEnumerator WaitForJoin()
        {
            // Wait for join to start. Item and map loading starts immediately afterwards
            while ((!NetworkLevelLoader.Instance.IsJoiningWorld || !NetworkLevelLoader.Instance.m_sequenceStarted) && !NetworkLevelLoader.Instance.m_failedJoin)
            {
                yield return null;
            }
            // If join failed, cancel
            if (NetworkLevelLoader.Instance.m_failedJoin)
            {
                Log.LogInfo("Joining room failed, no need to perform stack bug fix.");
                yield break;
            }
        }

        internal static void StorePlayerPositions()
        {
            // Store player positions and rotations so we can restore them after the map reload (by teleporting players)
            playerPositions.Clear();
            playerRotations.Clear();
            List<string> allPlayers = CharacterManager.Instance.PlayerCharacters.Values;
            foreach (var player in allPlayers)
            {
                var curChar = CharacterManager.Instance.GetCharacter(player);
                playerPositions.Add(player, curChar.transform.position);
                playerRotations.Add(player, curChar.transform.rotation);
            }
        }

        internal static void TeleportPlayers()
        {
            // Teleport players back to their previous locations
            List<string> allPlayers = CharacterManager.Instance.PlayerCharacters.Values;
            foreach (var player in allPlayers)
            {
                var curChar = CharacterManager.Instance.GetCharacter(player);
                if (playerPositions.ContainsKey(player) && playerRotations.ContainsKey(player))
                {
#if DEBUG
                    Log.LogInfo($"Teleporting character '{curChar.Name}' ({player}) to {playerPositions[player]}");
#endif
                    curChar.Teleport(playerPositions[player], playerRotations[player]);
                    if (curChar.CharacterCamera != null) curChar.CharacterCamera.ResetCameraToPlayer();
                }
#if DEBUG
                else
                {
                    Log.LogInfo($"Not teleporting character '{curChar.Name}' ({player}) because their position is not stored");
                }
#endif
            }
        }
    }

    public static class EnumExtensions
    {
        public static string GetDescription<T>(this T? enumerationValue)
    where T : struct
        {
            if (enumerationValue is null) return "null";

            Type type = enumerationValue.GetType();
            if (!type.IsEnum)
            {
                throw new ArgumentException("EnumerationValue must be of Enum type", "enumerationValue");
            }

            //Tries to find a DescriptionAttribute for a potential friendly name
            //for the enum
            MemberInfo[] memberInfo = type.GetMember(enumerationValue.ToString());
            if (memberInfo != null && memberInfo.Length > 0)
            {
                object[] attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);

                if (attrs != null && attrs.Length > 0)
                {
                    //Pull out the description value
                    return ((DescriptionAttribute)attrs[0]).Description;
                }
            }
            //If we have no description attribute, just return the ToString of the enum
            return enumerationValue.ToString();
        }
    }
}