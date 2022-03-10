using BepInEx;
using R2API;
using R2API.Utils;
using RoR2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine.Networking;

#pragma warning disable Publicizer001 // Accessing a member that was not originally public
namespace ExamplePlugin
{
	//This is an example plugin that can be put in BepInEx/plugins/ExamplePlugin/ExamplePlugin.dll to test out.
    //It's a small plugin that adds a relatively simple item to the game, and gives you that item whenever you press F2.

    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]
	
	//This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	
	//We will be using 2 modules from R2API: ItemAPI to add our item and LanguageAPI to add our language tokens.
    [R2APISubmoduleDependency(nameof(ItemAPI), nameof(LanguageAPI), nameof(DifficultyAPI))]
	
	//This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class ExamplePlugin : BaseUnityPlugin
	{
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        //If we see this PluginGUID as it is on thunderstore, we will deprecate this mod. Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "AuthorName";
        public const string PluginName = "ExamplePlugin";
        public const string PluginVersion = "1.0.0";

		//We need our item definition to persist through our functions, and therefore make it a class field.
        //private static ItemDef myItemDef;


        public GameObject pickupMystery;
        public GameObject[] pickupModels;

        public GameObject optionPickup;
        public DifficultyDef difficulty4Def;
        public DifficultyIndex difficulty4Index;

        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);

            //First let's define our item
            //myItemDef = ScriptableObject.CreateInstance<ItemDef>();

            // Language Tokens, check AddTokens() below.
            //myItemDef.name = "EXAMPLE_CLOAKONKILL_NAME";
            //myItemDef.nameToken = "EXAMPLE_CLOAKONKILL_NAME";
            //myItemDef.pickupToken = "EXAMPLE_CLOAKONKILL_PICKUP";
            //myItemDef.descriptionToken = "EXAMPLE_CLOAKONKILL_DESC";
            //myItemDef.loreToken = "EXAMPLE_CLOAKONKILL_LORE";

            //The tier determines what rarity the item is:
            //Tier1=white, Tier2=green, Tier3=red, Lunar=Lunar, Boss=yellow,
            //and finally NoTier is generally used for helper items, like the tonic affliction
            //myItemDef.tier = ItemTier.Tier2;

            //You can create your own icons and prefabs through assetbundles, but to keep this boilerplate brief, we'll be using question marks.
            //myItemDef.pickupIconSprite = Resources.Load<Sprite>("Textures/MiscIcons/texMysteryIcon");
            //myItemDef.pickupModelPrefab = Resources.Load<GameObject>("Prefabs/PickupModels/PickupMystery");

            //pickupMystery =  Resources.Load<GameObject>("Prefabs/PickupModels/PickupMystery");
            //pickupModels = Resources.LoadAll<GameObject>("Prefabs/PickupModels");

            //Can remove determines if a shrine of order, or a printer can take this item, generally true, except for NoTier items.
            //myItemDef.canRemove = true;

            //Hidden means that there will be no pickup notification,
            //and it won't appear in the inventory at the top of the screen.
            //This is useful for certain noTier helper items, such as the DrizzlePlayerHelper.
            //myItemDef.hidden = false;
			
            //Now let's turn the tokens we made into actual strings for the game:
            //AddTokens();

            //You can add your own display rules here, where the first argument passed are the default display rules: the ones used when no specific display rules for a character are found.
            //For this example, we are omitting them, as they are quite a pain to set up without tools like ItemDisplayPlacementHelper
            //var displayRules = new ItemDisplayRuleDict(null);

            //Then finally add it to R2API
            //ItemAPI.Add(new CustomItem(myItemDef, displayRules));

            //But now we have defined an item, but it doesn't do anything yet. So we'll need to define that ourselves.
            //GlobalEventManager.onCharacterDeathGlobal += GlobalEventManager_onCharacterDeathGlobal;

            // pathは途中のinteractablesやitemsを抜いたものになる
            this.optionPickup = Addressables.LoadAssetAsync<GameObject>(key: "RoR2/DLC1/OptionPickup/OptionPickup.prefab").WaitForCompletion();
            Logger.LogInfo(this.optionPickup);


            // make new Difficulty
            this.difficulty4Def = new DifficultyDef(5f, "DestinyDifficulty_4_NAME", "Step13", "DestinyDifficulty_5_DESCRIPTION",
                ColorCatalog.GetColor(ColorCatalog.ColorIndex.LunarCoin), "de", true);
            this.difficulty4Def.foundIconSprite = true;
            this.difficulty4Index = DifficultyAPI.AddDifficulty(this.difficulty4Def);
            //this.difficulty4Def.iconSprite = Base.GroovyAssetBundle.LoadAsset<Sprite>("texDifficulty4Icon.png");
            LanguageAPI.Add(this.difficulty4Def.nameToken, "Destiny");
            LanguageAPI.Add(this.difficulty4Def.descriptionToken, "Difficulty Scaling: <style=cIsHealth>+200%</style></style>");

            // Monsoonのスケーリング調節
            // default difficulty 1: 1f, 2: 2f, 3:3f
            // difficultyDefs[2]がmonsoon
            // DnSpyではreadonlyだが書き換えできる
            //DifficultyCatalog.difficultyDefs[2].scalingValue = 4f;
            Logger.LogInfo(DifficultyCatalog.difficultyDefs);


            // お金のスケーリング調整
            On.RoR2.Run.GetDifficultyScaledCost_int_float += (orig, self, baseCost, difficultyCoefficient) =>
            {
                //return (int)((float)baseCost * Mathf.Pow(difficultyCoefficient, 1.25f));
                return (int)((float)baseCost*1.25 * Mathf.Pow(difficultyCoefficient, 1.70f));
            };


            On.RoR2.ChestBehavior.ItemDrop += (orig, self) =>
            {
                Log.LogInfo("Item Drop from Chst!!");

                orig(self);
            };

            On.RoR2.InfiniteTowerWaveController.DropRewards += (orig, self) =>
            {
                GameObject prefab = self.rewardPickupPrefab;
                orig(self);
            };

            On.RoR2.ChestBehavior.Open += (orig, self) =>
            {
                Logger.LogInfo("open");
                orig(self);
            };

            On.RoR2.PickupPickerController.OnInteractionBegin += (orig, self, activator) =>
            {
                Logger.LogInfo(activator);

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
                    // 作成するOption list
                    HashSet<PickupPickerController.Option> list = new HashSet<PickupPickerController.Option>();
                    // item historyから追加
                    for (int i = 0; i < inventory.itemAcquisitionOrder.Count; i++)
                    {
                        ItemIndex itemIndex = inventory.itemAcquisitionOrder[i];
                        ItemDef itemDef = ItemCatalog.GetItemDef(itemIndex);
                        ItemTierDef itemTierDef = ItemTierCatalog.GetItemTierDef(itemDef.tier);
                        PickupIndex pickupIndex = PickupCatalog.FindPickupIndex(itemIndex);
                        //if ((!itemTierDef || itemTierDef.canScrap) && itemDef.canRemove && !itemDef.hidden && itemDef.DoesNotContainTag(ItemTag.Scrap))
                        // canScrapのみにすると、void itemが選ばれなくなる
                        if (itemDef.canRemove && !itemDef.hidden)
                        {
                            if (itemDef.tier == ItemTier.Tier2)
                            {
                                if (Random.value > 1 / 3)
                                    continue;
                            }
                            else if (itemDef.tier == ItemTier.Tier3)
                            {
                                if (Random.value > 1 / 10)
                                    continue;
                            }
                            list.Add(new PickupPickerController.Option
                            {
                                available = true,
                                pickupIndex = pickupIndex
                            });
                        }
                    }

                    // もともと設定してあったoptionsを追加
                    // かならず一つは加える
                    list.Add(self.options[0]);
                    //for (int i = 0; i < self.options.Length; i++)
                    //{
                    //    list.Add(self.options[i]);
                    //}
                    
                    // 3以下のときはもう1つ加える
                    if (self.options.Count() <= 3)
                    {
                        list.Add(self.options[1]);
                    }

                    // shuffleして先頭3つだけ残す
                    Random random = new Random();
                    PickupPickerController.Option[] rolledList = list.ToList().OrderBy(x => Random.value).Take(2).ToArray();

                    Logger.LogInfo(rolledList);
                    self.SetOptionsServer(rolledList);
                    self.contextString = "rolled";
                }

                // 先にoptionsの中身を変えてから、もとのOnInteractionBeginを呼び出す。
                orig(self, activator);
            };

            On.RoR2.ChestBehavior.ItemDrop += (orig, self) =>
            {
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

                if (!NetworkServer.active)
                {
                    Debug.LogWarning("[Server] function 'System.Void RoR2.ChestBehavior::ItemDrop()' called on client");
                    return;
                }
                if (self.dropPickup == PickupIndex.none || self.dropCount < 1)
                {
                    return;
                }
                float angle = 360f / (float)self.dropCount;
                Vector3 vector = Vector3.up * self.dropUpVelocityStrength + self.dropTransform.forward * self.dropForwardVelocityStrength;
                Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
                for (int i = 0; i < self.dropCount; i++)
                {
                    //PickupDropletController.CreatePickupDroplet(self.dropPickup, self.dropTransform.position + Vector3.up * 1.5f, vector);
                    //vector = rotation * vector;
                    //self.Roll();

                    PickupDropletController.CreatePickupDroplet(new GenericPickupController.CreatePickupInfo
                    {
                        pickupIndex = PickupCatalog.FindPickupIndex(ItemTier.Tier1),
                        pickerOptions = PickupPickerController.GenerateOptionsFromDropTable(3, self.dropTable, self.rng),
                        //pickerOptions = PickupPickerController.SetOptionsFromInteractor(),
                        rotation = Quaternion.identity,
                        prefabOverride = this.optionPickup
                    }, self.dropTransform.position + Vector3.up * 1.5f, vector);
                    vector = rotation * vector;
                    self.Roll();
                }
                self.dropPickup = PickupIndex.none;
            };





            // This line of log will appear in the bepinex console when the Awake method is done.
            Log.LogInfo(nameof(Awake) + " done.");
        }


        //This function adds the tokens from the item using LanguageAPI, the comments in here are a style guide, but is very opiniated. Make your own judgements!
        private void AddTokens()
        {
            //The Name should be self explanatory
            LanguageAPI.Add("EXAMPLE_CLOAKONKILL_NAME", "Cuthroat's Garb");

            //The Pickup is the short text that appears when you first pick this up. This text should be short and to the point, numbers are generally ommited.
            LanguageAPI.Add("EXAMPLE_CLOAKONKILL_PICKUP", "Chance to cloak on kill");

            //The Description is where you put the actual numbers and give an advanced description.
            LanguageAPI.Add("EXAMPLE_CLOAKONKILL_DESC", "Whenever you <style=cIsDamage>kill an enemy</style>, you have a <style=cIsUtility>5%</style> chance to cloak for <style=cIsUtility>4s</style> <style=cStack>(+1s per stack)</style.");
            
            //The Lore is, well, flavor. You can write pretty much whatever you want here.
            LanguageAPI.Add("EXAMPLE_CLOAKONKILL_LORE", "Those who visit in the night are either praying for a favour, or preying on a neighbour.");
        }

        //The Update() method is run on every frame of the game.
        private void Update()
        {
            //This if statement checks if the player has currently pressed F2.
            if (Input.GetKeyDown(KeyCode.F2))
            {
                //Get the player body to use a position:	
                var transform = PlayerCharacterMasterController.instances[0].master.GetBodyObject().transform;

                //And then drop our defined item in front of the player.

                Log.LogInfo($"Player pressed F2. Spawning our custom item at coordinates {transform.position}");
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
