using System;
using BepInEx;
using HarmonyLib;
using CardChoiceSpawnUniqueCardPatch.CustomCategories;
using UnboundLib.GameModes;
using System.Collections;
using UnboundLib.Utils;
using UnboundLib;
using System.Linq;
using System.Collections.Generic;
using BepInEx.Configuration;
using Photon.Pun;
using UnboundLib.Networking;


namespace DynamicCardMods
{
	// These are the mods required for our mod to work
	[BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)]
	[BepInDependency("pykess.rounds.plugins.moddingutils", BepInDependency.DependencyFlags.HardDependency)]
	[BepInDependency("pykess.rounds.plugins.cardchoicespawnuniquecardpatch", BepInDependency.DependencyFlags.HardDependency)]

	[BepInDependency("com.willuwontu.rounds.managers", BepInDependency.DependencyFlags.SoftDependency)]
	// Declares our mod to Bepin
	[BepInPlugin(ModId, ModName, Version)]
	// The game our mod is associated with
	[BepInProcess("Rounds.exe")]
	public class DynamicCardMods : BaseUnityPlugin
	{
		private const string ModId = "com.HibouGlorieux.Rounds.DynamicCardMods";
		internal const string ModName = "DynamicCardMods";
		public const string Version = "0.1.0"; // What version are we on (major.minor.patch)?

		internal bool bCurseActivated = false;

		static public DynamicCardMods instance { get; private set; }

		internal Dictionary<CardCategory, int> cardsPerMod = new Dictionary<CardCategory, int>();
		internal Dictionary<Player, CardCategory[]> blacklistedCategories = new Dictionary<Player, CardCategory[]>();

		public ConfigEntry<int> minimumAmountOfCardsPerPlayer;
		public ConfigEntry<int> minimumDifferentMods;
		public ConfigEntry<bool> bPoolIsShared;
		public ConfigEntry<bool> bIsActive;
		public ConfigEntry<string> baseCardMod1;
		public ConfigEntry<string> baseCardMod2;
		public ConfigEntry<string> baseCardMod3;

		internal int seed = 0;

		void Awake()
		{
			// Use this to call any harmony patch files your mod may have
			instance = this;

			minimumAmountOfCardsPerPlayer = Config.Bind(ModName, nameof(minimumAmountOfCardsPerPlayer), 75,
				"Minimum amount of cards for each player");
			minimumDifferentMods = Config.Bind(ModName, nameof(minimumDifferentMods), 1,
				"Minimum amount of card mods to be active");
			bIsActive = Config.Bind(ModName, nameof(bIsActive), false,
				"Activate/deactive the mod");
			bPoolIsShared = Config.Bind(ModName, nameof(bPoolIsShared), false,
				"Makes all player share the same pool of cards");
			baseCardMod1 = Config.Bind(ModName, nameof(baseCardMod1), "",
				"Mod1 that should always be here");
			baseCardMod2 = Config.Bind(ModName, nameof(baseCardMod2), "",
				"Mod1 that should always be here");
			baseCardMod3 = Config.Bind(ModName, nameof(baseCardMod3), "",
				"Mod1 that should always be here");

			var harmony = new Harmony(ModId);
			harmony.PatchAll();
		}
		void Start()
		{
			bCurseActivated = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.willuwontu.rounds.managers");
			OptionMenu.RegisterMenu();

			Unbound.RegisterHandshake(ModId, OnHandshakeCompleted);
			GameModeManager.AddHook(GameModeHooks.HookGameStart, OnGameStart);
			GameModeManager.AddHook(GameModeHooks.HookGameEnd, OnGameEnd);
		}

		private void OnHandshakeCompleted()
		{
			if (PhotonNetwork.IsMasterClient)
			{
				seed = UnityEngine.Random.Range(0, int.MaxValue);
				NetworkingManager.RPC_Others(typeof(DynamicCardMods), nameof(SyncSettings),
					new object[] {
						bPoolIsShared.Value,
						minimumAmountOfCardsPerPlayer.Value,
						seed
					});
			}
		}

		[UnboundRPC]
		private static void SyncSettings(bool bPoolShared, int minCards, int syncSeed)
		{
			instance.bPoolIsShared.Value = (bool)bPoolShared;
			instance.minimumAmountOfCardsPerPlayer.Value = (int)minCards;
			instance.seed = (int)syncSeed;
		}

		internal void GatherCardModsInfo()
		{
			Log("GatherCardModsInfo called !");
			blacklistedCategories.Clear();

			//Cards.active is all ACTIVE cards
			//CardManager is all cards including deactivated !
			cardsPerMod.Clear(); // redo in case it was edited between two games
			List<Card> cardsFromCardManager = CardManager.cards.Values.ToList();
			foreach (Card card in cardsFromCardManager)
			{
				CardCategory cardCategory = CustomCardCategories.instance.CardCategory("__pack-" + card.category);
				// add category while we're at it
				if (!card.cardInfo.categories.Contains(cardCategory))
					card.cardInfo.categories = card.cardInfo.categories.Append(cardCategory).ToArray();

				if (!card.enabled)
					continue;

				if (bCurseActivated)
					if (CurseHandler.IsACurse(card.cardInfo))
						continue; // don't add it to the card pool if it's cursed

				if (!cardsPerMod.ContainsKey(cardCategory))
					cardsPerMod[cardCategory] = 1;
				else
					cardsPerMod[cardCategory]++;
			}

			//clear if mod isn't present anymore
			bool b1Exist = false;
			bool b2Exist = false;
			bool b3Exist = false;
			foreach (CardCategory category in cardsPerMod.Keys)
			{
				if (category.name == baseCardMod1.Value)
					b1Exist = true;
				if (category.name == baseCardMod2.Value)
					b2Exist = true;
				if (category.name == baseCardMod3.Value)
					b3Exist = true;
			}
			if (!b1Exist)
				baseCardMod1.Value = "";
			if (!b2Exist)
				baseCardMod2.Value = "";
			if (!b3Exist)
				baseCardMod3.Value = "";

			/*
			foreach (var (mod, number) in cardsPerMod)
			{
				Log(String.Format("there are {0} cards in mod " + mod, number));
			}
			*/
		}

		private IEnumerator OnGameStart(IGameModeHandler gm)
		{
			if (!bIsActive.Value)
				yield break;
			GatherCardModsInfo();


			// we don't care if we're in local, if we're online then get the seed that has been given by masterClient
			int seedToUse = PhotonNetwork.OfflineMode ? UnityEngine.Random.Range(0, int.MaxValue) : seed;
			Log("my seed to use is: " + seedToUse);
			System.Random rand = new System.Random(seedToUse);
			// Hold on, should this run only on master or each player ?
			// Do i need to sync it ?
			// How to sync it ?
			if (bPoolIsShared.Value)
			{
				List<CardCategory> categoriesToBlacklist = MakeBlacklistedCategories(minimumAmountOfCardsPerPlayer.Value, rand);
				foreach (Player player in PlayerManager.instance.players)
				{
					Log("for player: " + player.playerID + " whitelist is: ");
					blacklistedCategories.Add(player, categoriesToBlacklist.ToArray());
					ModdingUtils.Extensions.CharacterStatModifiersExtension.GetAdditionalData(player.data.stats).blacklistedCategories.AddRange(categoriesToBlacklist);
				}

			}
			else foreach (Player player in PlayerManager.instance.players)
				{
					Log("for player: " + player.playerID + " whitelist is: ");
					List<CardCategory> categoriesToBlacklist = MakeBlacklistedCategories(minimumAmountOfCardsPerPlayer.Value, rand);
					blacklistedCategories.Add(player, categoriesToBlacklist.ToArray());
					ModdingUtils.Extensions.CharacterStatModifiersExtension.GetAdditionalData(player.data.stats).blacklistedCategories.AddRange(categoriesToBlacklist);
				}

			yield break;
		}

		//remove all blacklist
		private IEnumerator OnGameEnd(IGameModeHandler handler)
		{
			foreach (var (player, categories) in blacklistedCategories)
			{
				if (player)
					foreach (CardCategory category in categories)
						ModdingUtils.Extensions.CharacterStatModifiersExtension.GetAdditionalData(player.data.stats).blacklistedCategories.Remove(category);
			}
			blacklistedCategories.Clear();
			yield break;
		}

		private int GetTotalActiveCards()
		{
			int total = 0;
			foreach (var (category, numberOfCards) in cardsPerMod)
			{
				if (baseCardMod1.Value == category.name
				|| baseCardMod2.Value == category.name
				|| baseCardMod3.Value == category.name)
				{
					continue;
				}
				total += numberOfCards;
			}
			return total;
		}

		private List<CardCategory> MakeBlacklistedCategories(int minimumAmountOfCards, System.Random rand)
		{
			List<CardCategory> categoriesToBlacklist = new List<CardCategory>();
			int currentCardsAdded = 0;

			List<CardCategory> orderedKeys = cardsPerMod.Keys.OrderBy(k => k.name).ToList();
			//whitelist base mods
			orderedKeys.Remove(CustomCardCategories.instance.CardCategory(baseCardMod1.Value));
			orderedKeys.Remove(CustomCardCategories.instance.CardCategory(baseCardMod2.Value));
			orderedKeys.Remove(CustomCardCategories.instance.CardCategory(baseCardMod3.Value));

			int totalActiveCards = GetTotalActiveCards();
			if (totalActiveCards <= minimumAmountOfCards)
				return categoriesToBlacklist;

			int modsAdded = 0;

			while (currentCardsAdded < minimumAmountOfCards ||
				(modsAdded < minimumDifferentMods.Value || minimumDifferentMods.Value >= orderedKeys.Count)
				&& orderedKeys.Count > 0)
			{
				int randomIndex = rand.Next(orderedKeys.Count);
				CardCategory categoryToAdd = orderedKeys[randomIndex];

				Log(categoryToAdd.name);
				currentCardsAdded += cardsPerMod[categoryToAdd];
				orderedKeys.RemoveAt(randomIndex);
				modsAdded++;
			}
			// Only unwanted categories remain
			return orderedKeys;
		}

		internal void Log(string message)
		{
#if DEBUG
			UnityEngine.Debug.Log(ModName +": " + message);
#endif
		}

		void OnDestroy()
		{
			GameModeManager.RemoveHook(GameModeHooks.HookGameStart, OnGameStart);
		}
	}
}
