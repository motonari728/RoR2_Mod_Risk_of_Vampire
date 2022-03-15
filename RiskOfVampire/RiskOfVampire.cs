using BepInEx;
using R2API;
using R2API.Utils;
using R2API.Networking;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Networking;
using BepInEx.Configuration;
using MonoMod.Cil;
using System;
using R2API.Networking.Interfaces;

#pragma warning disable Publicizer001 // Accessing a member that was not originally public
namespace RiskOfVampire
{
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]

    //This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI), nameof(DifficultyAPI), nameof(DirectorAPI), nameof(NetworkingAPI))]

    //This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class.
    public class RiskOfVampire : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "mochi";
        // PluginNameに空白を入れると読み込まれない
        public const string PluginName = "RiskOfVampire";
        public const string PluginVersion = "1.0.0";

        //protected ConfigFile Config { get; }

        //We need our item definition to persist through our functions, and therefore make it a class field.
        //private static ItemDef myItemDef;

        //public GameObject pickupMystery;
        //public GameObject[] pickupModels;

        public static GameObject OptionPickup;
        public static Sprite MonsoonIcon;

        public DifficultyDef difficulty4Def;
        public DifficultyIndex difficulty4Index;
        public DifficultyDef difficulty45Def;
        public DifficultyIndex difficulty45Index;
        public DifficultyDef difficulty5Def;
        public DifficultyIndex difficulty5Index;

        public int ambientLevelFloor = 1;

        // config need to sync
        private static ConfigEntry<float> InvTime;
        private static ConfigEntry<float> OspPercent;
        private static ConfigEntry<float> HealPerSecond;
        private static ConfigEntry<float> PossessedItemChance;
        private static ConfigEntry<float> MoneyScaling;
        private static ConfigEntry<int> ItemPickerOptionAmount;
        private static ConfigEntry<int> WhiteItemUpperLimit;
        private static ConfigEntry<int> GreenItemUpperLimit;

        // server->client sync variable
        public static float invTime;
        public static float ospPercent;
        public static float healPerSecond;
        public static float possessedItemChance;
        public static float moneyScaling;
        public static int itemPickerOptionAmount;
        public static int whiteItemUpperLimit;
        public static int greenItemUpperLimit;

        // config don't need to sync
        private static ConfigEntry<float> MultiShopSpawnChance;
        private static ConfigEntry<float> ScrapperSpawnChance;
        private static ConfigEntry<float> PrinterSpawnChance;
        private static ConfigEntry<float> ChanceShrineSpawnChance;
        private static ConfigEntry<float> HealMultiply;


        // ゲームの起動時に呼ばれる
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);


            LoadAssets();
            DefineDifficulties();

            // Hooks
            HookMoneyScaling();
            Hook_PickupPicker_OnInteractionBegin();
            Hook_ChestBehavior();
            HookOSP();
            HooksForLog();
            HookMonsterSpawn();
            HookHeal();
            Hook_HPDefine();
            Hook_Spawns();

            NetworkingAPI.RegisterMessageType<SyncConfig>();


            On.RoR2.Run.Start += (orig, self) =>
            {
                Logger.LogInfo("Run start");
                // hostまたは、ソロプレイのとき
                if (NetworkServer.active)
                {
                    BindConfig();
                }
                orig(self);
            };

            //Log.LogInfo(nameof(Awake) + " done.");
        }

        private void Hook_Spawns()
        {
            // Remove Interactables
            On.RoR2.SceneDirector.GenerateInteractableCardSelection += SceneDirector_GenerateInteractableCardSelection;
        }

        private WeightedSelection<DirectorCard> SceneDirector_GenerateInteractableCardSelection(On.RoR2.SceneDirector.orig_GenerateInteractableCardSelection orig, SceneDirector self)
        {
            var weightedSelection = orig(self);
            for (var i = 0; i < weightedSelection.Count; i++)
            {
                var choiceInfo = weightedSelection.GetChoice(i);
                //var prefabName = choiceInfo.value.spawnCard.prefab.name;
                SpawnCard spawnCard = choiceInfo.value.spawnCard;

                //if (IsMultiShop(spawnCard) || IsScrapper(spawnCard) || IsPrinter(spawnCard) || IsChanceShrine(spawnCard))
                //{
                //    chestChance += choiceInfo.weight;
                //    choiceInfo.weight = 0f;
                //    weightedSelection.ModifyChoiceWeight(i, 0);
                //}

                // MultishopOnly を参考にしてる
                if (IsMultiShop(spawnCard))
                {
                    weightedSelection.ModifyChoiceWeight(i, choiceInfo.weight * MultiShopSpawnChance.Value);
                }
                if (IsScrapper(spawnCard))
                {
                    weightedSelection.ModifyChoiceWeight(i, choiceInfo.weight * ScrapperSpawnChance.Value);
                }
                if (IsPrinter(spawnCard))
                {
                    weightedSelection.ModifyChoiceWeight(i, choiceInfo.weight * PrinterSpawnChance.Value);
                }
                if (IsChanceShrine(spawnCard))
                {
                    weightedSelection.ModifyChoiceWeight(i, choiceInfo.weight * ChanceShrineSpawnChance.Value);
                }
            }
            return weightedSelection;
        }

        private bool IsMultiShop(SpawnCard spawnCard)
        {
            string a = spawnCard.name.ToLower();
            return a == DirectorAPI.Helpers.InteractableNames.MultiShopCommon.ToLower() || a == DirectorAPI.Helpers.InteractableNames.MultiShopUncommon.ToLower();
        }

        private bool IsScrapper(SpawnCard spawnCard)
        {
            string a = spawnCard.name.ToLower();
            return a == "iscScrapper".ToLower();
        }

        private bool IsPrinter(SpawnCard spawnCard)
        {
            string a = spawnCard.name.ToLower();
            return a == DirectorAPI.Helpers.InteractableNames.PrinterCommon.ToLower() || a == DirectorAPI.Helpers.InteractableNames.PrinterUncommon.ToLower() || a == DirectorAPI.Helpers.InteractableNames.PrinterLegendary.ToLower();
        }

        private bool IsChanceShrine(SpawnCard spawnCard)
        {
            string a = spawnCard.name.ToLower();
            return a == DirectorAPI.Helpers.InteractableNames.ChanceShrine.ToLower();
        }

        private static void Hook_HPDefine()
        {
            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {

                // デフォルトの難易度だったら、何もしない
                // デフォルト難易度は0~, Invalidが-1, 追加難易度は-2から負に向かう
                if ((int)Run.instance.selectedDifficulty >= -1)
                {
                    orig(self);
                    return;
                }

                // レベルアップでのHP増加量の調整
                if (self.isPlayerControlled)
                {
                    // デフォルトは全部* 0.3f
                    self.levelMaxHealth = Mathf.Round(self.baseMaxHealth * 0.3f * 1.5f);

                    // levelMaxShieldはそもそも0なので、0のままにする
                    //self.levelMaxShield = Mathf.Round(self.baseMaxShield * 0.3f * 1.5f);       
                }
                orig(self);
            };
        }

        private void HookHeal()
        {
            On.RoR2.HealthComponent.OnInventoryChanged += (orig, self) =>
            {
                // healPerSecondが1なら、もとの挙動のまま
                if (healPerSecond == 1f)
                {
                    orig(self);
                    return;
                }
                // repeatHealComponentの付け外しを除去
                // それ以外の部分を移植
                self.itemCounts = default(HealthComponent.ItemCounts);
                Inventory inventory = self.body.inventory;
                self.itemCounts = (inventory ? new HealthComponent.ItemCounts(inventory) : default(HealthComponent.ItemCounts));
                self.currentEquipmentIndex = (inventory ? inventory.currentEquipmentIndex : EquipmentIndex.None);
            };

            On.RoR2.HealthComponent.Heal += (orig, self, amount, procChainMask, nonRegen) =>
            {
                // Void Fungus = MushroomVoid, id 119
                // Void Fungusはきちんと経時ヒールの対象になってて、10%制限に引っかかってるから良し！
                if (!NetworkServer.active)
                {
                    return 0f;
                }

                // 追加難易度かつnonRegenかつRepeatHealでない場合
                // デフォルト難易度は0~, Invalidが-1, 追加難易度は-2から負に向かう

                // 回復量の調節
                // すでにDifficultyAPIでcountAsHardModeにしてあるから、Regenerationは*0.6倍になってる
                // そこでRegen以外を調節する

                // if (difficultyDef.countAshardMode)
                // characterMaster.inventory.GiveItem(RoR2Content.Items.MonsoonPlayerHelper, 1);
                // MonsoonPlayerHelper>1のとき、RecalculateStatsでRegenを*0.6
                if (nonRegen && (int)Run.instance.selectedDifficulty <= -2 && !procChainMask.HasProc(ProcType.RepeatHeal))
                {
                    amount *= HealMultiply.Value;
                }

                // healPerSecondが1のときはもとの挙動のまま
                if (healPerSecond == 1f)
                {
                    return orig(self, amount, procChainMask, nonRegen);
                }

                // コープスブルーム(repeatHeal)の経時回復機能を使う
                // repeatHealComponentがついてなかったらつける
                if (self.body.teamComponent.teamIndex == TeamIndex.Player && self.repeatHealComponent == null)
                {
                    self.repeatHealComponent = base.gameObject.AddComponent<HealthComponent.RepeatHealComponent>();
                    self.repeatHealComponent.healthComponent = self;
                }
                // もとのHeal()から転記。回復しちゃいけないやつの除外
                if (!self.alive || amount <= 0f || self.body.HasBuff(RoR2Content.Buffs.HealingDisabled))
                {
                    return 0f;
                }
                float num = self.health;
                bool flag = false;
                // 何か不明だが、回復してないのでそのまま残す
                if (self.currentEquipmentIndex == RoR2Content.Equipment.LunarPotion.equipmentIndex && !procChainMask.HasProc(ProcType.LunarPotionActivation))
                {
                    self.potionReserve += amount;
                    return amount;
                }

                // RepeatHealじゃない場合
                // repeatHeal用に後で使うために保存する
                // この中では、条件分岐せずに必ずreturn 0f;で終わらせたい。
                if (nonRegen && self.repeatHealComponent && !procChainMask.HasProc(ProcType.RepeatHeal))
                {
                    // repeatHealはProcType.CritHealを持ってないので、ここで加工する
                    if (nonRegen && !procChainMask.HasProc(ProcType.CritHeal) && Util.CheckRoll(self.body.critHeal, self.body.master))
                    {
                        procChainMask.AddProc(ProcType.CritHeal);
                        flag = true;
                    }
                    if (flag)
                    {
                        amount *= 2f;
                    }
                    if (flag)
                    {
                        GlobalEventManager.instance.OnCrit(self.body, null, self.body.master, amount / self.fullHealth * 10f, procChainMask);
                    }

                    // FixedUpdateからのHealはprocChainMaskがRepeatHealになってる
                    //if (nonRegen && this.repeatHealComponent && !procChainMask.HasProc(ProcType.RepeatHeal))
                    // HealperSecond
                    self.repeatHealComponent.healthFractionToRestorePerSecond = healPerSecond / (1f + (float)self.itemCounts.repeatHeal);
                    self.repeatHealComponent.AddReserve(amount * (float)(1 + self.itemCounts.repeatHeal), self.fullHealth * 2f);
                    return 0f;
                }

                // repeatHeal()はorigに任せる
                // onCharacterHealServerのイベントがorigからしか呼べないので、origを呼び出さないといけない
                return orig(self, amount, procChainMask, nonRegen);
            };
        }

        private void HookMonsterSpawn()
        {
            On.RoR2.CombatDirector.AttemptSpawnOnTarget += (orig, self, spawnTarget, placementMode) =>
            {
                // この関数はspawnできるか全モンスターをチェックしてself.spawn()を叩いてる
                bool isSpawned = orig(self, spawnTarget, placementMode);
                if (isSpawned == true)
                {
                    if (this.ambientLevelFloor <= 9)
                    {
                        // origの中でthis.monsterCredit -= (float)self.currentMonsterCard.cost;される
                        // costを戻してやる
                        // spawn * 1.5
                        self.monsterCredit += (float)self.currentMonsterCard.cost / 2f;
                    }
                    // 15レベル以降は増やしすぎるときつい
                    else if (this.ambientLevelFloor <= 15)
                    {
                        // spawn * 1.25
                        self.monsterCredit += (float)self.currentMonsterCard.cost * (1f / 4f);
                    }
                }
                return isSpawned;
            };

            On.RoR2.Run.OnAmbientLevelUp += (orig, self) =>
            {
                // 現在の敵のレベルかな
                Logger.LogInfo(self.ambientLevelFloor);
                this.ambientLevelFloor = self.ambientLevelFloor;
                orig(self);
            };
        }

        private static void HookMoneyScaling()
        {
            On.RoR2.Run.GetDifficultyScaledCost_int_float += (orig, self, baseCost, difficultyCoefficient) =>
            {
                //return (int)((float)baseCost * Mathf.Pow(difficultyCoefficient, 1.25f));
                return (int)((float)baseCost * Mathf.Pow(difficultyCoefficient, moneyScaling));
            };
        }

        private static void HooksForLog()
        {
            //On.RoR2.InfiniteTowerWaveController.DropRewards += (orig, self) =>
            //{
            //    GameObject prefab = self.rewardPickupPrefab;
            //    orig(self);
            //};
        }

        [Server]
        private void BindConfig()
        {
            // ここはソロプレイでも呼ばれるので気をつける
            PossessedItemChance = Config.Bind("Chance", "Possessed Item Chance", 0.75f, 
                new ConfigDescription("The probability that your owned item is added to the item picker's item candidates."));
            OspPercent = Config.Bind("OSP", "OSP Threshold", 0.8f, 
                new ConfigDescription("Max receive damage / Max HP. Vanilla is 0.9"));
            InvTime = Config.Bind("OSP", "Invulnerable Time", 0.5f, 
                new ConfigDescription("The amount of time a player remains invulnerable after one shot protection is triggered. Vanilla is 0.1."));
            MoneyScaling = Config.Bind("Scaling", "Money Scaling", 1.45f,
                new ConfigDescription("How much money needed for opening chests. Normal 1.25. Code: `baseCost * Mathf.Pow(difficultyCoefficient, moneyScaling)`"));
            HealPerSecond = Config.Bind("Stats", "Max Heal per second", 1f,
                new ConfigDescription("Max Heal per second. Store overflow to next seconds. Store limit is 200% HP. Enter 1.0 to return to the original behavior."));

            MultiShopSpawnChance = Config.Bind("Spawn", "MultiShop spawn chance", 0.3f,
                new ConfigDescription("Multiply the spawn weight of MultiShop. 0 is None. 1 is Original weight"));
            ScrapperSpawnChance = Config.Bind("Spawn", "Scrapper spawn chance", 0.0f,
                new ConfigDescription("Multiply the spawn weight of Scrapper. 0 is None. 1 is Original weight"));
            PrinterSpawnChance = Config.Bind("Spawn", "3D Printer spawn chance", 0.3f,
                new ConfigDescription("Multiply the spawn weight of 3D Printer. 0 is None. 1 is Original weight"));
            ChanceShrineSpawnChance = Config.Bind("Spawn", "LuckShrine spawn chance", 0.1f,
                new ConfigDescription("Multiply the spawn weight of LuckShrine. 0 is None. 1 is Original weight"));

            HealMultiply = Config.Bind("Stats", "Healing amount modify", 0.5f,
                new ConfigDescription("Multiply the amount of healing. If you enter 0.6, amount of all heal excluding regen will be 60%. Only valid for additional difficulty"));
            ItemPickerOptionAmount = Config.Bind("Item", "Option amount of ItemPicker", 3,
                new ConfigDescription("How many candidates are displayed when opeingItemPicker orb spawned from chests."));

            ReloadConfig();
        }

        private void ReloadConfig()
        {
            // ここはソロプレイでも呼ばれるので気をつける
            invTime = InvTime.Value;
            ospPercent = OspPercent.Value;
            healPerSecond = HealPerSecond.Value;
            possessedItemChance = PossessedItemChance.Value;
            moneyScaling = MoneyScaling.Value;
            itemPickerOptionAmount = ItemPickerOptionAmount.Value;

            // Clientに送信
            if (NetworkServer.active)
            {
                new SyncConfig(possessedItemChance, ospPercent, invTime, moneyScaling, healPerSecond, itemPickerOptionAmount).Send(NetworkDestination.Clients);
            }
        }

        private void HookOSP()
        {
            // OSPの無敵時間を設定
            On.RoR2.HealthComponent.TriggerOneShotProtection += (orig, self) =>
            {
                if (!NetworkServer.active)
                {
                    return;
                }
                orig(self);
                self.ospTimer = invTime;
                Logger.LogInfo(self.ospTimer);
            };

            On.RoR2.CharacterBody.RecalculateStats += (orig, self) =>
            {
                // ospFractionはHP満タンから最大ダメージを受けたときに残る量
                // これ以上のダメージを受けたときは、ospTimer(1秒)の間だけ無敵になる
                // orig()の中でospFraction = 0.1f;に設定後、書き換えられてる。
                // this.oneShotProtectionFraction = Mathf.Max(0f, this.oneShotProtectionFraction - (1f - 1f / this.cursePenalty));
                orig(self);

                // もっと高い値に設定し直す
                // 1 - 0.9 = 0.1
                // 1 - 0.4 = 0.6
                float newOspFraction = 1f - ospPercent;
                // もとがif(NetworkServer.active)の外側なので、ifいらない
                self.oneShotProtectionFraction = Mathf.Max(0f, newOspFraction - (1f - 1f / self.cursePenalty));
            };
        }

        private void LoadAssets()
        {
            // pathは途中のinteractablesやitemsを抜いたものになる
            // 実際どこがいらないのかはよくわからん
            OptionPickup = Addressables.LoadAssetAsync<GameObject>(key: "RoR2/DLC1/OptionPickup/OptionPickup.prefab")
                .WaitForCompletion();
            //Logger.LogInfo(this.optionPickup);
            MonsoonIcon = Addressables.LoadAssetAsync<Sprite>(key: "RoR2/Junk/Common/texDifficulty2.png")
                .WaitForCompletion();
            Logger.LogInfo(MonsoonIcon);
        }

        private void Hook_ChestBehavior()
        {
            On.RoR2.ChestBehavior.Open += (orig, self) =>
            {
                //Logger.LogInfo("open");
                orig(self);
            };

            On.RoR2.ChestBehavior.ItemDrop += (orig, self) =>
            {
                if (!NetworkServer.active)
                {
                    return;
                }
                if (!self.gameObject.name.Contains("Chest"))
                {
                    orig(self);
                    return;
                }
                if (self.gameObject.name.Contains("Lunar") || self.gameObject.name.Contains("Void"))
                {
                    orig(self);
                    return;
                }
                if (self.gameObject.name.Contains("Equipment"))
                {
                    orig(self);
                    return;
                }
                if (self.dropPickup == RoR2.PickupIndex.none || self.dropCount < 1)
                {
                    return;
                }
                float angle = 360f / (float)self.dropCount;
                Vector3 vector = Vector3.up * self.dropUpVelocityStrength + self.dropTransform.forward * self.dropForwardVelocityStrength;
                Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
                for (int i = 0; i < self.dropCount; i++)
                {
                    // dropTableからの候補の生成
                    // これが所持アイテムと合わさって最終的な候補となる
                    PickupPickerController.Option[] pickerOptions = PickupPickerController.GenerateOptionsFromDropTable(3, self.dropTable, self.rng);

                    // dropletの色を抽選結果の最低値のTierの色にする
                    ItemTier lowestItemTier = ItemTier.Tier3;
                    foreach(PickupPickerController.Option option in pickerOptions)
                    {
                        ItemTier itemTier = option.pickupIndex.pickupDef.itemTier;
                        if (itemTier == ItemTier.Tier1 || itemTier == ItemTier.VoidTier1)
                        {
                            lowestItemTier = ItemTier.Tier1;
                            break;
                        }
                        if (itemTier == ItemTier.Tier2 || itemTier == ItemTier.VoidTier2)
                        {
                            lowestItemTier = ItemTier.Tier2;
                        }
                    }
                    PickupDropletController.CreatePickupDroplet(new GenericPickupController.CreatePickupInfo
                    {
                        pickupIndex = PickupCatalog.FindPickupIndex(lowestItemTier),
                        pickerOptions = pickerOptions,
                        //pickerOptions = PickupPickerController.SetOptionsFromInteractor(),
                        rotation = Quaternion.identity,
                        prefabOverride = OptionPickup
                    }, self.dropTransform.position + Vector3.up * 1.5f, vector);
                    vector = rotation * vector;
                    self.Roll();
                }
                self.dropPickup = PickupIndex.none;
            };
        }

        private void Hook_PickupPicker_OnInteractionBegin()
        {
            On.RoR2.PickupPickerController.OnInteractionBegin += (orig, self, activator) =>
            {
                // SetOptionsFromInteractor()から内容をコピーした
                //Logger.LogInfo(activator);
                //self.
                if (self.gameObject.name.Contains("Scrapper"))
                {
                    orig(self, activator);
                    return;
                }
                if (self.gameObject.name != "OptionPickup(Clone)")
                {
                    orig(self, activator);
                    return;
                }
                if (!activator)
                {
                    Debug.Log("No activator.");
                    orig(self, activator);
                    return;
                }
                CharacterBody component = activator.GetComponent<CharacterBody>();
                if (!component)
                {
                    Debug.Log("No body.");
                    orig(self, activator);
                    return;
                }
                Inventory inventory = component.inventory;
                if (!inventory)
                {
                    Debug.Log("No inventory.");
                    orig(self, activator);
                    return;
                }

                // 抽選開始
                if (self.contextString != "rolled")
                {
                    ItemTier lowestItemTier = ItemTier.Tier3;
                    foreach(PickupPickerController.Option option in self.options)
                    {
                        ItemTier itemTier = option.pickupIndex.pickupDef.itemTier;
                        if (itemTier == ItemTier.Tier1 || itemTier == ItemTier.VoidTier1)
                        {
                            lowestItemTier = ItemTier.Tier1;
                            break;
                        }
                        if (itemTier == ItemTier.Tier2 || itemTier == ItemTier.VoidTier2)
                        {
                            lowestItemTier = ItemTier.Tier2;
                        }
                    }

                    // 作成するOption list
                    HashSet<PickupPickerController.Option> list = new HashSet<PickupPickerController.Option>();
                    // item historyから追加
                    for (int i = 0; i < inventory.itemAcquisitionOrder.Count; i++)
                    {
                        ItemIndex itemIndex = inventory.itemAcquisitionOrder[i];
                        ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                        ItemTierDef itemTierDef = ItemTierCatalog.GetItemTierDef(itemDef.tier);
                        PickupIndex pickupIndex = PickupCatalog.FindPickupIndex(itemIndex);
                        // canScrapのみにすると、void itemが選ばれなくなる
                        if (!itemDef)
                        {
                            continue;
                        }
                        // スクラップアイテムの除去
                        if (itemDef.ContainsTag(ItemTag.Scrap))
                        {
                            continue;
                        }
                        if (!itemDef.canRemove || itemDef.hidden)
                        {
                            continue;
                        }
                        // 使用済みアイテムはNoTier
                        if (itemDef.tier == ItemTier.NoTier || itemDef.tier == ItemTier.Lunar)
                        {
                            continue;
                        }
                        // 追加確率の抽選
                        if (possessedItemChance > UnityEngine.Random.value)
                        {
                            continue;
                        }
                        // 最小がTier3の場合
                        if (lowestItemTier == ItemTier.Tier3)
                        {
                            if (itemDef.tier == ItemTier.Tier3 || itemDef.tier == ItemTier.VoidTier3)
                            {
                                // allowed.  Proceed to the next.
                            } else
                            {
                                continue;
                            }
                        }
                        // 最小がTier2の場合
                        if (lowestItemTier == ItemTier.Tier2)
                        {
                            ItemTier[] allowed = new ItemTier[] { ItemTier.Tier2, ItemTier.VoidTier2, ItemTier.Tier3, ItemTier.VoidTier3 };
                            if (allowed.Contains(itemDef.tier))
                            {
                                // allowed. Proceed to the next.
                            } else
                            {
                                continue;
                            }
                        }
                        // 最小がTier1の場合
                        if (lowestItemTier != ItemTier.Tier1)
                        {

                            if (itemDef.tier == ItemTier.Tier2 || itemDef.tier == ItemTier.VoidTier2)
                            {
                                if (UnityEngine.Random.value > 1 / 3)
                                    continue;
                            }
                            else if (itemDef.tier == ItemTier.Tier3 || itemDef.tier == ItemTier.VoidTier3
                                || itemDef.tier == ItemTier.Boss || itemDef.tier == ItemTier.VoidBoss)
                            {
                                if (UnityEngine.Random.value > 1 / 10)
                                    continue;
                            }
                        }
                        // 条件を生き残ったやつを候補に追加する
                        list.Add(new PickupPickerController.Option
                        {
                            available = true,
                            pickupIndex = pickupIndex
                        });
                    }

                    // 3以下のときはもう1つ加える
                    // 別の人が開けた場合、optionsは2つしか入っていない。３つ目はないものとして扱うこと！
                    //if (self.options.Count() <= 3)
                    //{
                    //    list.Add(self.options[1]);
                    //}

                    // 以前のoptionsを全部加える
                    for (var i = 0; i < self.options.Length; i++)
                    {
                        list.Add(self.options[i]);
                    }

                    // shuffleして先頭nつだけ残す
                    PickupPickerController.Option[] rolledList = list.ToList().OrderBy(x => UnityEngine.Random.value)
                        .Take(itemPickerOptionAmount).ToArray();

                    //Logger.LogInfo(rolledList);
                    self.SetOptionsServer(rolledList);
                    self.contextString = "rolled";
                }

                // 先にoptionsの中身を変えてから、もとのOnInteractionBeginを呼び出す。
                orig(self, activator);
            };
        }

        private void DefineDifficulties()
        {
            // make new Difficulties
            // Monsoonのスケーリング調節
            // default difficulty 1: 1f, 2: 2f, 3:3f
            // difficultyDefs[2]がmonsoon
            // DnSpyではreadonlyだが書き換えできる

            this.difficulty4Def = new DifficultyDef(4f, "DestinyDifficulty_4_NAME", "Step13", "DestinyDifficulty_4_DESCRIPTION",
                ColorCatalog.GetColor(ColorCatalog.ColorIndex.LunarCoin), "de", true);
            this.difficulty4Def.foundIconSprite = true;
            this.difficulty4Def.iconSprite = MonsoonIcon;
            this.difficulty4Index = DifficultyAPI.AddDifficulty(this.difficulty4Def);
            LanguageAPI.Add(this.difficulty4Def.nameToken, "Destiny");
            LanguageAPI.Add(this.difficulty4Def.descriptionToken, "<style=cStack>>Health Regeneration: <style=cIsHealth>-40%</style> \n>Difficulty Scaling: <style=cIsHealth>+100%</style></style>");

            this.difficulty45Def = new DifficultyDef(4.5f, "DestinyDifficulty_45_NAME", "Step13", "DestinyDifficulty_45_DESCRIPTION",
                ColorCatalog.GetColor(ColorCatalog.ColorIndex.LunarCoin), "de", true);
            this.difficulty45Def.foundIconSprite = true;
            this.difficulty45Def.iconSprite = MonsoonIcon;
            this.difficulty45Index = DifficultyAPI.AddDifficulty(this.difficulty45Def);
            LanguageAPI.Add(this.difficulty45Def.nameToken, "Destiny");
            LanguageAPI.Add(this.difficulty45Def.descriptionToken, "<style=cStack>>Health Regeneration: <style=cIsHealth>-40%</style> \n>Difficulty Scaling: <style=cIsHealth>+125%</style></style>");

            this.difficulty5Def = new DifficultyDef(5f, "DestinyDifficulty_5_NAME", "Step13", "DestinyDifficulty_5_DESCRIPTION",
                ColorCatalog.GetColor(ColorCatalog.ColorIndex.LunarCoin), "de", true);
            this.difficulty5Def.foundIconSprite = true;
            this.difficulty5Def.iconSprite = MonsoonIcon;
            this.difficulty5Index = DifficultyAPI.AddDifficulty(this.difficulty5Def);
            LanguageAPI.Add(this.difficulty5Def.nameToken, "Destiny");
            LanguageAPI.Add(this.difficulty5Def.descriptionToken, "<style=cStack>>Health Regeneration: <style=cIsHealth>-40%</style> \n>Difficulty Scaling: <style=cIsHealth>+150%</style></style>");

            //Logger.LogInfo(DifficultyCatalog.difficultyDefs);
        }



        //The Update() method is run on every frame of the game.
        private void Update()
        {
            //This if statement checks if the player has currently pressed F2.
            if (Input.GetKeyDown(KeyCode.F5))
            {
                //Get the player body to use a position:	
                //var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

                Log.LogInfo("Player pressed F5.");
                // configをreloadして反映させる
                if (NetworkServer.active)
                {
                    Config.Reload();
                    ReloadConfig();
                }

                Logger.LogInfo("Config Reload finished");


                //PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(myItemDef.itemIndex), transform.position, transform.forward * 20f);


                //var catalogPath = "C:/Program Files (x86)/Steam/steamapps/common/Risk of Rain 2/Risk of Rain 2_Data/StreamingAssets/aa/catalog.json";
                //var bundlePath = "C:/Program Files (x86)/Steam/steamapps/common/Risk of Rain 2/Risk of Rain 2_Data/StreamingAssets/aa/StandaloneWindows64/ror2-base-parent_bin_assets_all";

                //var bundlePath = Application.streamingAssetsPath + "/aa/StandaloneWindows64/ror2-base-parent_bin_assets_all.bundle";

                //var loadedBundle = AssetBundle.LoadFromFile(bundlePath);


                //var prefab = Addressables.LoadAsset<GameObject>(bundlePath);

                //AsyncOperationHandle<IResourceLocator> catalog = Addressables.LoadContentCatalog(catalogPath);
                //catalog = Addressables.LoadContentCatalogAsync(catalogPath);


                //Addressables.LoadAssetAsync<GameObject>(catalogPath).Completed += gameObject =>
                //{
                //    //Logger.LogInfo(gameObject);
                //};


                //PickupDropletController.CreatePickupDroplet(
                //    new GenericPickupController.CreatePickupInfo
                //    {
                //        pickupIndex = PickupCatalog.FindPickupIndex(myItemDef.itemIndex),
                //        pickerOptions = PickupPickerController.GenerateOptionsFromDropTable(3, 
                //            this.rewardDropTable, this.rng),
                //        rotation = Quaternion.identity,
                //    }
                //);

                //GameObject pickupMystery =  Resources.Load<GameObject>("Prefabs/PickupModels/PickupMystery");
                //GameObject[] pickupModels = Resources.LoadAll<GameObject>("");
                //foreach (var pickupModel in pickupModels)
                //{
                //    //Debug.Log(pickupModel.name);
                //}
                //Log.LogInfo(pickupModels);
            }
        }
    }
}

#pragma warning restore Publicizer001 // Accessing a member that was not originally public
