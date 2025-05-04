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

		public ConfigEntry<bool> bIsActive;
		public ConfigEntry<bool> bPoolIsShared;
		public ConfigEntry<int> minimumAmountOfCardsPerPlayer;
		public ConfigEntry<int> minimumDifferentMods;
		public ConfigEntry<bool> bKeepSameOnRematch;
		System.Random random = new System.Random();
		public List<ConfigEntry<string>> baseCardMods = new List<ConfigEntry<string>>(10);

		internal int seed = 0;

		void Awake()
		{
			// Use this to call any harmony patch files your mod may have
			instance = this;

			bIsActive = Config.Bind(ModName, nameof(bIsActive), true,
				"Activate/deactive the mod");
			minimumAmountOfCardsPerPlayer = Config.Bind(ModName, nameof(minimumAmountOfCardsPerPlayer), 150,
				"Minimum amount of cards for each player");
			minimumDifferentMods = Config.Bind(ModName, nameof(minimumDifferentMods), 1,
				"Minimum amount of card mods to be active");
			bPoolIsShared = Config.Bind(ModName, nameof(bPoolIsShared), false,
				"Makes all player share the same pool of cards");
			bKeepSameOnRematch = Config.Bind(ModName, nameof(bKeepSameOnRematch), false,
				"Keep the same card mods upon rematch");
			for (int i = 0; i < baseCardMods.Capacity; i++)
				baseCardMods.Add(Config.Bind(ModName, nameof(baseCardMods) + i, "",
				"Mod that should always be here"));

			var harmony = new Harmony(ModId);
			harmony.PatchAll();
		}
		void Start()
		{
			bCurseActivated = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.willuwontu.rounds.managers");
			OptionMenu.RegisterMenu();

			seed = UnityEngine.Random.Range(0, int.MaxValue);
			random = new Random(seed);
			Unbound.RegisterHandshake(ModId, OnHandshakeCompleted);
			GameModeManager.AddHook(GameModeHooks.HookGameStart, OnGameStart);
			GameModeManager.AddHook(GameModeHooks.HookGameEnd, OnGameEnd);
		}

		private void OnHandshakeCompleted()
		{
			if (PhotonNetwork.IsMasterClient)
			{
				seed = UnityEngine.Random.Range(0, int.MaxValue);
				random = new System.Random(seed);

				string[] baseMods = baseCardMods.Select(configEntry => (configEntry.Value)).ToArray();
				NetworkingManager.RPC_Others(typeof(DynamicCardMods), nameof(SyncSettings),
					new object[] {
						seed,
						bIsActive.Value,
						bPoolIsShared.Value,
						bKeepSameOnRematch.Value,
						minimumAmountOfCardsPerPlayer.Value,
						minimumDifferentMods.Value,
						baseMods
					});
			}
		}

		[UnboundRPC]
		private static void SyncSettings(int syncSeed, bool bActive, bool bPoolShared, bool bKeepSameOnRematch, int minCards, int minMods, string[] baseMods)
		{
			instance.seed = syncSeed;
			instance.bIsActive.Value = bActive;
			instance.bPoolIsShared.Value = bPoolShared;
			instance.bKeepSameOnRematch.Value = bKeepSameOnRematch;
			instance.minimumAmountOfCardsPerPlayer.Value = minCards;
			instance.minimumDifferentMods.Value = minMods;
			for (int i = 0; i < baseMods.Length; i++)
				instance.baseCardMods[i].Value = baseMods[i];

			instance.random = new System.Random(instance.seed);
		}

		internal void GatherCardModsInfo()
		{
			// redo in case it was edited between two games
			blacklistedCategories.Clear();
			cardsPerMod.Clear();

			//Cards.active is all ACTIVE cards
			//CardManager is all cards including deactivated !
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

			//clear settings if mod isn't present anymore

			for (int i = 0; i < baseCardMods.Count; i++)
			{
				if (!cardsPerMod.Any(tuple => (tuple.Key.name == baseCardMods[i].Value)))
					baseCardMods[i].Value = "";
			}

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
			if (bKeepSameOnRematch.Value)
				random = new System.Random(seed);
			if (bKeepSameOnRematch.Value)
				random = new System.Random(seed);
			// Hold on, should this run only on master or each player ?
			// Do i need to sync it ?
			// How to sync it ?
			if (bPoolIsShared.Value)
			{
				List<CardCategory> categoriesToBlacklist = MakeBlacklistedCategories(minimumAmountOfCardsPerPlayer.Value);
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
					List<CardCategory> categoriesToBlacklist = MakeBlacklistedCategories(minimumAmountOfCardsPerPlayer.Value);
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
				if (baseCardMods.Any(configEntry => (configEntry.Value == category.name)))
					continue;
				total += numberOfCards;
			}
			return total;
		}

		private List<CardCategory> MakeBlacklistedCategories(int minimumAmountOfCards)
		{
			List<CardCategory> categoriesToBlacklist = new List<CardCategory>();
			int currentCardsAdded = 0;

			// gather all categories
			List<CardCategory> blacklist = cardsPerMod.Keys.OrderBy(k => k.name).ToList();

			// remove whitelisted base cardMods
			for (int i = 0; i < baseCardMods.Count; i++)
				blacklist.Remove(CustomCardCategories.instance.CardCategory(baseCardMods[i].Value));

			int totalActiveCards = GetTotalActiveCards();
			if (totalActiveCards <= minimumAmountOfCards)
				return categoriesToBlacklist;

			int modsAdded = 0;

			// get a random mod to whitelist and remove it from orderedKeys
			while ((currentCardsAdded < minimumAmountOfCards ||
				(modsAdded < minimumDifferentMods.Value || minimumDifferentMods.Value >= blacklist.Count))
				&& blacklist.Count > 0)
			{
				int randomIndex = random.Next(blacklist.Count);
				CardCategory categoryToAdd = blacklist[randomIndex];

				Log(categoryToAdd.name);
				currentCardsAdded += cardsPerMod[categoryToAdd];
				blacklist.RemoveAt(randomIndex);
				modsAdded++;
			}
			// Only unwanted categories remain
			return blacklist;
		}

		static internal void Log(string message)
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
