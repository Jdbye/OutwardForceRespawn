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
    public static class UIHelper
    {
        public static GameObject AddButton(GameObject baseButton, string name, int index, string title, UnityAction onClick)
        {
            GameObject newButton = GameObject.Instantiate(baseButton, baseButton.transform.parent);
            newButton.name = name;
            newButton.transform.SetSiblingIndex(index);
            Text textComponent = newButton.GetComponentInChildren<Text>();
            GameObject.Destroy(textComponent.GetComponent<UILocalize>());
            textComponent.text = title;
            Button button = newButton.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);
            return newButton;
        }
    }

    [HarmonyPatch(typeof(PauseMenu))]
    public static class PauseMenuPatches
    {
        [HarmonyPatch(nameof(PauseMenu.StartInit)), HarmonyPostfix]
        private static void PauseMenu_StartInit_Postfix(PauseMenu __instance)
        {
            try
            {
                Transform buttonsPanelTransform = __instance.m_hideOnPauseButtons.transform;
                GameObject settingsButton = buttonsPanelTransform.Find("btnOptions").gameObject;

                UIHelper.AddButton(settingsButton, "btnForceRespawn", 5, "Respawn", () =>
                {
                    ForceRespawnPlugin.Instance.StartCoroutine(ForceRespawnPlugin.ReloadMap(ForceRespawnPlugin.SpawnType.Value));
                });

                GameObject background = __instance.transform.FindInAllChildren("BG").gameObject;
                RectTransform bgTransform = background.GetComponent<RectTransform>();
                bgTransform.anchorMin -= new Vector2(0, 0.1f);
            }
            catch (Exception e)
            {
                ForceRespawnPlugin.Log.LogError($"Failed to patch PauseMenu UI for player {__instance.CharacterUI.TargetCharacter.Name} ({e.GetType()}): {e.Message}");
                ForceRespawnPlugin.Log.LogDebug($"Stack trace: {e.StackTrace}");
            }
        }
    }
}
