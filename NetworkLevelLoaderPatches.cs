using Epic.OnlineServices;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ForceRespawn
{
    [HarmonyPatch(typeof(NetworkLevelLoader))]
    public static class NetworkLevelLoaderPatches
    {
        [HarmonyPatch(nameof(NetworkLevelLoader.OnJoinedRoom)), HarmonyPostfix]
        private static void NetworkLevelLoader_OnJoinedRoom_Postfix(NetworkLevelLoader __instance)
        {
            // Force a respawn/map reload to fix the stack bug
            try
            {
                if (ForceRespawnPlugin.EnableInventoryStackBugFix.Value)
                {
                    if (PhotonNetwork.isNonMasterClientInRoom) // if we are not the host
                    {
                        ForceRespawnPlugin.Instance.StartCoroutine(ForceRespawnPlugin.ReloadMapAfterJoin());
                    }
                }
            }
            catch (Exception e)
            {
                ForceRespawnPlugin.Log.LogError($"NetworkLevelLoader.OnJoinedRoom Postfix failed! ({e.GetType()}): {e.Message}");
                ForceRespawnPlugin.Log.LogDebug($"Stack trace: {e.StackTrace}");
            }
        }
    }
}
