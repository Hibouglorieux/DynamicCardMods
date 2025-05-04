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
		static private int[] warningOnCardAmountIndexes = null;
		public static void RegisterMenu()
		{
			Unbound.RegisterMenu("Dynamic CardMods", () => { }, CreateMenuUI, null);
		}

		private static void UpdateUI()
		{
			//WholeUI check
			bool bVisible = DynamicCardMods.instance.bIsActive.Value;
			foreach (GameObject option in options)
			{
				// this moves the title as child are not taken into account from their verticalBox
				//option.SetActive(bVisible);
				option.transform.localScale = bVisible ? Vector3.one : Vector3.zero;
			}

			//Warning check
			int nbOfBaseMods = DynamicCardMods.instance.baseCardMods.Count(configEntry => (configEntry.Value != ""));
			bool bSetWarningActive = nbOfBaseMods < 1 && DynamicCardMods.instance.minimumAmountOfCardsPerPlayer.Value < 60;
			foreach (int index in warningOnCardAmountIndexes)
				options[index].SetActive(bSetWarningActive);
		}

		private static void UpdateBaseCardMods(bool shoulBeBase, string cardCategoryName, int toggleIndex)
		{
			if (!shoulBeBase)
			{
				for (int i = 0; i < DynamicCardMods.instance.baseCardMods.Count; i++)
				{
					if (DynamicCardMods.instance.baseCardMods[i].Value == cardCategoryName)
						DynamicCardMods.instance.baseCardMods[i].Value = "";

				}
			}
			if (shoulBeBase)
			{
				bool bAdded = false;
				for (int i = 0; i < DynamicCardMods.instance.baseCardMods.Count; i++)
				{
					if (DynamicCardMods.instance.baseCardMods[i].Value == "")
					{
						DynamicCardMods.instance.baseCardMods[i].Value = cardCategoryName;
						bAdded = true;
						break;
					}
				}

				if (!bAdded)
				{
					options[toggleIndex].GetComponent<Toggle>().isOn = false;
					DynamicCardMods.Log("Only " + DynamicCardMods.instance.baseCardMods.Count + " max base card mods");
				}
			}
		}

		private static void CreateMenuUI(GameObject menu)
		{
			options.Clear();
			warningOnCardAmountIndexes = new int[3];
			MenuHandler.CreateText(DynamicCardMods.ModName + " Options", menu, out TextMeshProUGUI _);
			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);

			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			MenuHandler.CreateToggle(DynamicCardMods.instance.bIsActive.Value, "Active",
				menu, (bool bNewValue) =>
				{
					DynamicCardMods.instance.bIsActive.Value = bNewValue;
					UpdateUI();
				});

			warningOnCardAmountIndexes[0] = options.Count;
			options.Add(MenuHandler.CreateText("Warning: very low amount can result in almost no choice if you", menu, out TextMeshProUGUI _, 30));
			warningOnCardAmountIndexes[1] = options.Count;
			options.Add(MenuHandler.CreateText("have no card mods enabled at all time", menu, out TextMeshProUGUI _, 30));
			warningOnCardAmountIndexes[2] = options.Count;
			options.Add(MenuHandler.CreateText("(minimum amount should be around 60 without no card mods enabled at all time)", menu, out TextMeshProUGUI _, 20));
			options.Add(MenuHandler.CreateSlider("Minimum amount of cards in players card pool", menu, 30, 1, 2000,
				DynamicCardMods.instance.minimumAmountOfCardsPerPlayer.Value,
				(float newValue) => {
					DynamicCardMods.instance.minimumAmountOfCardsPerPlayer.Value = (int)newValue;
					UpdateUI();
				}, out UnityEngine.UI.Slider _, true));

			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			options.Add(MenuHandler.CreateSlider("Minimum amount of different mods in players card pool", menu, 30, 1, 10, DynamicCardMods.instance.minimumDifferentMods.Value,
				(float newValue) => { DynamicCardMods.instance.minimumDifferentMods.Value = (int)newValue; }, out UnityEngine.UI.Slider _, true));

			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			options.Add(MenuHandler.CreateToggle(!DynamicCardMods.instance.bPoolIsShared.Value, "Each player has their own individual pool of cards",
				menu, (bool bNewValue) => { DynamicCardMods.instance.bPoolIsShared.Value = !bNewValue; }));

			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			options.Add(MenuHandler.CreateToggle(DynamicCardMods.instance.bKeepSameOnRematch.Value, "Keep the same card mods pool on rematch",
				menu, (bool bNewValue) => { DynamicCardMods.instance.bKeepSameOnRematch.Value = bNewValue; }));


			MenuHandler.CreateText("", menu, out TextMeshProUGUI _);
			options.Add(MenuHandler.CreateText("Chose card mods to be there at all time", menu, out TextMeshProUGUI _, 50));
			options.Add(MenuHandler.CreateText("(these mods are not taken into account for minimum values above)", menu, out TextMeshProUGUI _, 25));
			options.Add(MenuHandler.CreateText("(maximum amount is " + DynamicCardMods.instance.baseCardMods.Count +")", menu, out TextMeshProUGUI _, 25));

			DynamicCardMods.instance.GatherCardModsInfo();
			int i = options.Count;
			foreach (CardCategory category in DynamicCardMods.instance.cardsPerMod.Keys.OrderBy(elem => (elem.name)).ToArray())
			{
				int indexOfToggle = i;
				string categoryName = category.name;

				options.Add(MenuHandler.CreateToggle(
					DynamicCardMods.instance.baseCardMods.Any(configEntry => configEntry.Value == categoryName)
					? true : false,
					category.name.Substring("__pack-".Length),
					menu, (bool bNewValue) => {
						UpdateBaseCardMods(bNewValue, categoryName, indexOfToggle);
						UpdateUI();
					}));

				i++;
			}

			UpdateUI();
		}
	}
}
