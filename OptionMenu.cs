using System;
using UnboundLib;
using UnboundLib.Utils.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;

namespace DynamicCardMods
{
	internal static class OptionMenu
	{

		static private List<GameObject> options = new List<GameObject>();
		public static void RegisterMenu()
		{
			Unbound.RegisterMenu(DynamicCardMods.ModName, () => { }, CreateMenuUI, null);
		}

		private static void ActivateUI(bool bVisible)
		{
			foreach (GameObject option in options)
			{
				//option.SetActive(bVisible);
				option.transform.localScale = bVisible ? Vector3.zero : Vector3.one;
			}
		}

		private static void UpdateBaseCardMods(bool shoulBeBase, string cardCategoryName, int toggleIndex)
		{
			DynamicCardMods.instance.Log("called update with toggleIndex: ");
			if (!shoulBeBase)
			{
				if (DynamicCardMods.instance.baseCardMod1.Value == cardCategoryName)
					DynamicCardMods.instance.baseCardMod1.Value = "";
				if (DynamicCardMods.instance.baseCardMod2.Value == cardCategoryName)
					DynamicCardMods.instance.baseCardMod2.Value = "";
				if (DynamicCardMods.instance.baseCardMod3.Value == cardCategoryName)
					DynamicCardMods.instance.baseCardMod3.Value = "";
			}
			if (shoulBeBase)
			{
				if (DynamicCardMods.instance.baseCardMod1.Value == "")
					DynamicCardMods.instance.baseCardMod1.Value = cardCategoryName;
				else if (DynamicCardMods.instance.baseCardMod2.Value == "")
					DynamicCardMods.instance.baseCardMod2.Value = cardCategoryName;
				else if (DynamicCardMods.instance.baseCardMod3.Value == "")
					DynamicCardMods.instance.baseCardMod3.Value = cardCategoryName;
				else
				{
					options[toggleIndex].GetComponent<Toggle>().isOn = false;
					DynamicCardMods.instance.Log("Only 3 max base card mods");
				}

			}
		}

		private static void CreateMenuUI(GameObject menu)
		{
			options.Clear();
			MenuHandler.CreateText(DynamicCardMods.ModName + " Options", menu, out TextMeshProUGUI _);
			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			
			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			MenuHandler.CreateToggle(!DynamicCardMods.instance.bIsActive.Value, "DeactivateMod",
				menu, (bool bNewValue) => { DynamicCardMods.instance.bIsActive.Value = bNewValue;
					ActivateUI(bNewValue);
				});

			options.Add(MenuHandler.CreateSlider("Minimum amount of cards in players card pool", menu, 30, 75, 2000,
				DynamicCardMods.instance.minimumAmountOfCardsPerPlayer.Value,
				(float newValue) => { DynamicCardMods.instance.minimumAmountOfCardsPerPlayer.Value = (int)newValue; }, out UnityEngine.UI.Slider _, true));

			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			options.Add(MenuHandler.CreateSlider("Minimum amount of different mods in players card pool", menu, 30, 1, 10, DynamicCardMods.instance.minimumDifferentMods.Value,
				(float newValue) => { DynamicCardMods.instance.minimumDifferentMods.Value = (int)newValue; }, out UnityEngine.UI.Slider _, true));

			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			options.Add(MenuHandler.CreateToggle(!DynamicCardMods.instance.bPoolIsShared.Value, "Each player has their own individual pool of cards",
				menu, (bool bNewValue) => { DynamicCardMods.instance.bPoolIsShared.Value = bNewValue; }));


			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			MenuHandler.CreateText("Chose up to 3 card mods to be there everytime", menu, out TextMeshProUGUI _, 50);
			MenuHandler.CreateText("(these mods are not taken into account for minimum values above)", menu, out TextMeshProUGUI _, 40);
			DynamicCardMods.instance.GatherCardModsInfo();
			int i = options.Count;
			foreach (CardCategory category in DynamicCardMods.instance.cardsPerMod.Keys)
			{
				int indexOfToggle = i;
				string categoryName = category.name;

				options.Add(MenuHandler.CreateToggle(
					category.name == DynamicCardMods.instance.baseCardMod1.Value
					|| category.name == DynamicCardMods.instance.baseCardMod2.Value
					|| category.name == DynamicCardMods.instance.baseCardMod3.Value
					? true : false,
					category.name.Substring("__pack-".Length),
					menu, (bool bNewValue) => { UpdateBaseCardMods(bNewValue, categoryName, indexOfToggle); }));
				i++;
			}
			DynamicCardMods.instance.Log("Went fine");
		}
	}
}
