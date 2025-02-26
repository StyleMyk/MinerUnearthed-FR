﻿using System;
using System.Collections.Generic;
using BepInEx;
using R2API;
using R2API.Utils;
using EntityStates;
using RoR2;
using RoR2.Skills;
using UnityEngine;
using UnityEngine.Networking;
using KinematicCharacterController;
using EntityStates.Digger;
using BepInEx.Configuration;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Permissions;
using Modules;
using DiggerUnearthed.Module;

[module: UnverifiableCode]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace DiggerPlugin {
    [BepInDependency("com.bepis.r2api", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rob.Aatrox", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.rob.Direseeker", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.DestroyedClone.AncientScepter", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.KomradeSpectre.Aetherium", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Sivelos.SivsItems", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.K1454.SupplyDrop", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.Skell.GoldenCoastPlus", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("com.TeamMoonstorm.Starstorm2", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    [BepInPlugin(MODUID, "DiggerUnearthed", "1.9.5")]
    [R2APISubmoduleDependency(new string[]
    {
        "PrefabAPI",
        "LoadoutAPI",
        "UnlockableAPI",
        "DirectorAPI",
        nameof(RecalculateStatsAPI),
        nameof(DamageAPI)
    })]

    public class DiggerPlugin : BaseUnityPlugin
    {
        public const string MODUID = "com.rob.DiggerUnearthed";

        public static DiggerPlugin instance;
        public static BepInEx.Logging.ManualLogSource logger;

        public static GameObject characterBodyPrefab;
        // I do not know why I needed this hack
        // paladin was able to grab the CharacterModel from characterPrefab.GetComponentInChildren just fine
        //      but for some reason i can do that fine while i'm setting miner up, but it gets lost by the time i'm setting up item displays
        //          it's not that it's broken. it works at one point and breaks later, which is much more infuriating
        // I'd normally be in a whatever this works kinda mood but I'm very very curious what the actual code difference is that changes this. the new fancy modern prefab builder is basically the exact same code as is here
        // thanks for coming to my ted talk
        // have a lovely evening
        public static CharacterModel characterPrefabModel;
        public static GameObject characterDisplay;

        public GameObject doppelganger;

        public static DamageAPI.ModdedDamageType ToTheStarsClassicDamage;
        public static GameObject ToTheStarsClassicEffectPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OmniEffect/OmniExplosionVFXQuick");

        public static DamageAPI.ModdedDamageType CleaveDamage;

        public static readonly Color characterColor = new Color(1f, 0.9062671f, 0.5613208f);

        public SkillLocator skillLocator;

        public static float adrenalineCap;

        public static GameObject backblastEffect;
        public static GameObject crushExplosionEffect;

        public static SkillDef specialSkillDef;
        public static SkillDef scepterSpecialSkillDef;

        public static SkillDef specialClassicSkillDef;
        public static SkillDef scepterSpecialClassicSkillDef;

        public static bool hasAatrox = false;
        public static bool direseekerInstalled = false;
        public static bool ancientScepterInstalled = false;
        public static bool classicItemsInstalled = false;
        public static bool aetheriumInstalled = false;
        public static bool sivsItemsInstalled = false;
        public static bool supplyDropInstalled = false;
        public static bool goldenCoastInstalled = false;
        public static bool starstormInstalled = false;
        public static uint blacksmithSkinIndex = 5;

        public static ConfigEntry<bool> forceUnlock;
        public static ConfigEntry<float> maxAdrenaline;
        public static ConfigEntry<bool> extraSkins;
        public static ConfigEntry<bool> styleUI;
        public static ConfigEntry<bool> enableDireseeker;
        public static ConfigEntry<bool> enableDireseekerSurvivor;
        public static ConfigEntry<bool> fatAcrid;

        public static ConfigEntry<float> gougeDamage;
        public static ConfigEntry<float> crushDamage;

        public static ConfigEntry<float> drillChargeDamage;
        public static ConfigEntry<float> drillChargeCooldown;

        public static ConfigEntry<float> drillBreakDamage;
        public static ConfigEntry<float> drillBreakCooldown;

        public static ConfigEntry<KeyCode> restKeybind;
        public static ConfigEntry<KeyCode> tauntKeybind;
        public static ConfigEntry<KeyCode> jokeKeybind;

        private void Awake()
        {
            SetupModCompat();
            Files.PluginInfo = Info;
            ConfigShit();
            SetupDamageTypes();
            LanguageTokens.RegisterLanguageTokens();
        }

        private void Start() {
            Logger.LogInfo("[Initializing Miner]");

            SoundBanks.Init();

            instance = this;
            logger = base.Logger;

            Assets.PopulateAssets();


            Unlockables.RegisterUnlockables();
            CreateDisplayPrefab();
            CreatePrefab();
            RegisterCharacter();

            Buffs.RegisterBuffs();
            Skins.RegisterSkins();
            RegisterEffects();

            CreateDoppelganger();

            Direseeker.CreateDireseeker();

            ItemDisplays.InitializeItemDisplays();

            Hook();

            new Modules.ContentPacks().Initialize();

            //RoR2.RoR2Application.onLoad += LateSetup;
            RoR2.ContentManagement.ContentManager.onContentPacksAssigned += LateSetup; ;

            //RoR2.ContentManagement.ContentManager.onContentPacksAssigned += LateSetup;

            AddStyle();

            Logger.LogInfo("[Initialized]");
        }

        private void SetupModCompat() {
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.TeamMoonstorm.Starstorm2")) {
                starstormInstalled = true;
            }

            //direseeker compat
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rob.Direseeker")) {
                direseekerInstalled = true;
            }

            //aetherium item displays- dll won't compile without a reference to aetherium
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.KomradeSpectre.Aetherium")) {
                aetheriumInstalled = true;
            }

            //aetherium item displays- dll won't compile without a reference to aetherium
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.Skell.GoldenCoastPlus")) {
                goldenCoastInstalled = true;
            }
            //sivs item displays- dll won't compile without a reference
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.Sivelos.SivsItems")) {
                sivsItemsInstalled = true;
            }
            //sivs item displays- dll won't compile without a reference
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.K1454.SupplyDrop")) {
                supplyDropInstalled = true;
            }
            //scepter stuff- dll won't compile without a reference to TILER2 and ClassicItems
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.DestroyedClone.AncientScepter")) {
                ancientScepterInstalled = true;
            }

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.ThinkInvisible.ClassicItems"))
            {
                classicItemsInstalled = true;
            }

            FixItemDisplays();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void FixItemDisplays() {
            ItemAPI.DoNotAutoIDRSFor("MinerBody");
        }

        private void LateSetup(HG.ReadOnlyArray<RoR2.ContentManagement.ReadOnlyContentPack> obj) {
            ItemDisplays.SetItemDisplays();
        }


        private void AddStyle()
        {
            if (styleUI.Value && BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.rob.Aatrox"))
            {
                //hasAatrox = true;
                //AddStyleMeter();
            }

            //Direseeker.LateSetup();
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ScepterSetup()
        {
            AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(scepterSpecialClassicSkillDef, "MinerBody", SkillSlot.Special, 0);
            AncientScepter.AncientScepterItem.instance.RegisterScepterSkill(scepterSpecialSkillDef, "MinerBody", SkillSlot.Special, 1);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void ScepterClassicSetup()
        {
            ThinkInvisible.ClassicItems.Scepter.instance.RegisterScepterSkill(scepterSpecialSkillDef, "MinerBody", SkillSlot.Special, specialSkillDef);
            ThinkInvisible.ClassicItems.Scepter.instance.RegisterScepterSkill(scepterSpecialClassicSkillDef, "MinerBody", SkillSlot.Special, specialClassicSkillDef);
        }

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void AddStyleMeter()
        {
            //characterPrefab.AddComponent<Aatrox.GenericStyleController>();
        }

        //shit is right
        private void ConfigShit()
        {
            forceUnlock = 
                base.Config.Bind<bool>("01 - General Settings",
                                       "Force Unlock",
                                       false, 
                                       "Unlocks the Miner by default");
            maxAdrenaline = 
                base.Config.Bind<float>("01 - General Settings", 
                                        "Adrenaline Cap", 
                                        50, 
                                        "Max Adrenaline stacks allowed");
            adrenalineCap = maxAdrenaline.Value - 1;

            extraSkins =
                base.Config.Bind<bool>("01 - General Settings", 
                                       "Extra Skins", 
                                       false, 
                                       "Enables a bunch of extra skins");
            styleUI = 
                base.Config.Bind<bool>("01 - General Settings", 
                                       "Style Rank", 
                                       true, 
                                       "Enables a style ranking system taken from Devil May Cry (only if Aatrox is installed as well)");
            enableDireseeker =
                base.Config.Bind<bool>("01 - General Settings", 
                                       "Enable Direseeker", 
                                       true, 
                                       "Enables the new boss");
            enableDireseekerSurvivor = 
                base.Config.Bind<bool>("01 - General Settings",
                                       "Direseeker Survivor", 
                                       false,
                                       "Enables the new boss as a survivor?");
            fatAcrid = 
                base.Config.Bind<bool>("01 - General Settings",
                                       "Perro Grande", 
                                       false, 
                                       "Enables fat Acrid as a lategame scav-tier boss");

            restKeybind = 
                base.Config.Bind<KeyCode>("02 - Keybinds", 
                                          "Rest Emote", 
                                          KeyCode.Alpha1, 
                                          "Keybind used for the Rest emote");
            tauntKeybind =
                base.Config.Bind<KeyCode>("02 - Keybinds", 
                                          "Taunt Emote",
                                          KeyCode.Alpha2, 
                                          "Keybind used for the Taunt emote");
            jokeKeybind = 
                base.Config.Bind<KeyCode>("02 - Keybinds", 
                                          "Joke Emote", 
                                          KeyCode.Alpha3,
                                          "Keybind used for the Joke emote");

            gougeDamage = 
                base.Config.Bind<float>("03 - Gouge 1.8.0", 
                                        "Damage", 
                                        2.7f, 
                                        "Damage coefficient");
            //Gouge.damageCoefficient = gougeDamage.Value;

            crushDamage = 
                base.Config.Bind<float>("04 - Crush 1.6.7",
                                        "Demage",
                                        3.6f, 
                                        "Damage coefficient");
            //Crush.damageCoefficient = crushDamage.Value;

            drillChargeDamage = 
                base.Config.Bind<float>("05 - Drill Charge",
                                        "Damage",
                                        1.8f, 
                                        "Damage coefficient per hit");
            drillChargeCooldown = 
                base.Config.Bind<float>("05 - Drill Charge",
                                        "Cooldown", 
                                        7f, 
                                        "Base cooldown");
            //DrillCharge.damageCoefficient = drillChargeDamage.Value;

            drillBreakDamage = 
                base.Config.Bind<float>("06 - Drill Crack Hammer",
                                        "Damage", 
                                        2f, 
                                        "Damage coefficient");
            //DrillBreak.damageCoefficient = drillBreakDamage.Value;

            drillBreakCooldown = 
                base.Config.Bind<float>("06 - Crack Hammer", 
                                        "Cooldown",
                                        3f, 
                                        "Base cooldown");
        }

        private void RegisterEffects()
        {
            backblastEffect = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("prefabs/effects/omnieffect/OmniExplosionVFX"), "MinerBackblastEffect", true);

            if (!backblastEffect.GetComponent<EffectComponent>()) backblastEffect.AddComponent<EffectComponent>();
            if (!backblastEffect.GetComponent<VFXAttributes>()) backblastEffect.AddComponent<VFXAttributes>();
            if (!backblastEffect.GetComponent<NetworkIdentity>()) backblastEffect.AddComponent<NetworkIdentity>();

            backblastEffect.GetComponent<VFXAttributes>().vfxPriority = VFXAttributes.VFXPriority.Always;

            
            Modules.Content.AddEffect(backblastEffect);

            crushExplosionEffect = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("prefabs/effects/omnieffect/OmniExplosionVFX"), "DiggerCrushExplosionEffect", true);

            if (!crushExplosionEffect.GetComponent<EffectComponent>()) crushExplosionEffect.AddComponent<EffectComponent>();
            if (!crushExplosionEffect.GetComponent<VFXAttributes>()) crushExplosionEffect.AddComponent<VFXAttributes>();
            if (!crushExplosionEffect.GetComponent<NetworkIdentity>()) crushExplosionEffect.AddComponent<NetworkIdentity>();

            crushExplosionEffect.GetComponent<VFXAttributes>().vfxPriority = VFXAttributes.VFXPriority.Always;
            crushExplosionEffect.GetComponent<EffectComponent>().applyScale = true;
            crushExplosionEffect.GetComponent<EffectComponent>().parentToReferencedTransform = true;

            Modules.Content.AddEffect(crushExplosionEffect);
        }

        private void Hook()
        {
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
            On.RoR2.SceneDirector.Start += SceneDirector_Start;

            //On.RoR2.PingerController.AttemptPing += PingerController_AttemptPing;
        }

        private void PingerController_AttemptPing(On.RoR2.PingerController.orig_AttemptPing orig, PingerController self, Ray aimRay, GameObject bodyObject) {

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "dampcavesimple") {
                aimRay = new Ray(new Vector3(76, -14.5f, -526), Vector3.down);
            }
            orig(self, aimRay, bodyObject);
        }

        private void SceneDirector_Start(On.RoR2.SceneDirector.orig_Start orig, SceneDirector self)
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "dampcavesimple")
            {
                GameObject hammer = Instantiate(Assets.blacksmithHammer);
                hammer.transform.position = new Vector3(76, -143.4f, -526);
                hammer.transform.rotation = Quaternion.Euler(new Vector3(340, 340, 70));
                hammer.transform.localScale = new Vector3(200, 200, 200);

                GameObject anvil = Instantiate(Assets.blacksmithAnvil);
                anvil.transform.position = new Vector3(74, -142.7f, -531);
                anvil.transform.localEulerAngles = new Vector3(293, 90, 270);
                anvil.transform.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            }

            orig(self);
        }


        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            int cleaveCount= sender.GetBuffCount(Buffs.cleaveBuff);
            args.armorAdd -= 3f * cleaveCount;

            int goldRushCount = sender.GetBuffCount(Buffs.goldRushBuff);
            if (goldRushCount > 0)
            {
                args.attackSpeedMultAdd += 0.1f * goldRushCount;
                args.baseMoveSpeedAdd += 0.15f * goldRushCount;
                args.baseRegenAdd += 0.25f * goldRushCount;
            }
        }

        //classic
        //wait this is retarded what
        private static GameObject CreateModel(int index)
        {

            GameObject model = null;

            if (index == 0) model = Assets.mainAssetBundle.LoadAsset<GameObject>("mdlMiner");
            else if (index == 1) model = Assets.mainAssetBundle.LoadAsset<GameObject>("MinerDisplay");


            return model;
        }

        private static void CreateDisplayPrefab()
        {
            //spaghetti incoming
            GameObject tempDisplay = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody"), "MinerDisplay");

            GameObject model = CreateModel(1);

            GameObject gameObject = new GameObject("ModelBase");
            gameObject.transform.parent = tempDisplay.transform;
            gameObject.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = new Vector3(1f, 1f, 1f);

            GameObject gameObject2 = new GameObject("CameraPivot");
            gameObject2.transform.parent = gameObject.transform;
            gameObject2.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            gameObject2.transform.localRotation = Quaternion.identity;
            gameObject2.transform.localScale = Vector3.one;

            GameObject gameObject3 = new GameObject("AimOrigin");
            gameObject3.transform.parent = gameObject.transform;
            gameObject3.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            gameObject3.transform.localRotation = Quaternion.identity;
            gameObject3.transform.localScale = Vector3.one;

            Transform transform = model.transform;
            transform.parent = gameObject.transform;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            ModelLocator modelLocator = tempDisplay.GetComponent<ModelLocator>();
            modelLocator.modelTransform = transform;
            modelLocator.modelBaseTransform = gameObject.transform;

            ChildLocator childLocator = model.GetComponent<ChildLocator>();

            CharacterModel characterModel = model.AddComponent<CharacterModel>();
            characterModel.body = null;
            characterModel.baseRendererInfos = new CharacterModel.RendererInfo[]
            {
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = model.GetComponentInChildren<SkinnedMeshRenderer>().material.SetHotpooMaterial(),
                    renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("DiamondPickL").GetComponent<MeshRenderer>().material.SetHotpooMaterial(),
                    renderer = childLocator.FindChild("DiamondPickL").GetComponent<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("DiamondPickR").GetComponent<MeshRenderer>().material.SetHotpooMaterial(),
                    renderer = childLocator.FindChild("DiamondPickR").GetComponent<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("JokeC4").GetComponentInChildren<MeshRenderer>().material.SetHotpooMaterial(),
                    renderer = childLocator.FindChild("JokeC4").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                }
            };
            characterModel.autoPopulateLightInfos = true;
            characterModel.invisibilityCount = 0;
            characterModel.temporaryOverlays = new List<TemporaryOverlay>();

            characterModel.SetFieldValue("mainSkinnedMeshRenderer", characterModel.baseRendererInfos[0].renderer.gameObject.GetComponent<SkinnedMeshRenderer>());

            characterDisplay = tempDisplay.GetComponent<ModelLocator>().modelBaseTransform.gameObject;

            characterDisplay.AddComponent<MenuSound>();
        }


        void Update() {
            if (Input.GetKeyDown(KeyCode.N) && Input.GetKeyDown(KeyCode.LeftAlt)) {
                var nig = Instantiate(characterBodyPrefab, null);
            }
        }


        private static void CreatePrefab()
        {
            characterBodyPrefab = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody"), "MinerBody");

            characterBodyPrefab.GetComponent<NetworkIdentity>().localPlayerAuthority = true;

            Destroy(characterBodyPrefab.transform.Find("ModelBase").gameObject);
            Destroy(characterBodyPrefab.transform.Find("CameraPivot").gameObject);
            Destroy(characterBodyPrefab.transform.Find("AimOrigin").gameObject);

            GameObject model = CreateModel(0);

            GameObject modelBase = new GameObject("ModelBase");
            modelBase.transform.parent = characterBodyPrefab.transform;
            modelBase.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            modelBase.transform.localRotation = Quaternion.identity;
            modelBase.transform.localScale = new Vector3(1f, 1f, 1f);

            GameObject cameraPivot = new GameObject("CameraPivot");
            cameraPivot.transform.parent = modelBase.transform;
            cameraPivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            cameraPivot.transform.localRotation = Quaternion.identity;
            cameraPivot.transform.localScale = Vector3.one;

            GameObject aimOrigin = new GameObject("AimOrigin");
            aimOrigin.transform.parent = modelBase.transform;
            aimOrigin.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            aimOrigin.transform.localRotation = Quaternion.identity;
            aimOrigin.transform.localScale = Vector3.one;

            model.transform.parent = modelBase.transform;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            CharacterDirection characterDirection = characterBodyPrefab.GetComponent<CharacterDirection>();
            characterDirection.moveVector = Vector3.zero;
            characterDirection.targetTransform = modelBase.transform;
            characterDirection.overrideAnimatorForwardTransform = null;
            characterDirection.rootMotionAccumulator = null;
            characterDirection.modelAnimator = model.GetComponentInChildren<Animator>();
            characterDirection.driveFromRootRotation = false;
            characterDirection.turnSpeed = 720f;

            CharacterBody bodyComponent = characterBodyPrefab.GetComponent<CharacterBody>();
            bodyComponent.name = "MinerBody";
            bodyComponent.baseNameToken = "MINER_NAME";
            bodyComponent.subtitleNameToken = "MINER_SUBTITLE";
            bodyComponent.bodyFlags = CharacterBody.BodyFlags.ImmuneToExecutes;
            bodyComponent.rootMotionInMainState = false;
            bodyComponent.mainRootSpeed = 0;
            bodyComponent.baseMaxHealth = 140;
            bodyComponent.levelMaxHealth = bodyComponent.baseMaxHealth * 0.3f;
            bodyComponent.baseRegen = 1f;
            bodyComponent.levelRegen = bodyComponent.baseRegen * 0.2f;
            bodyComponent.baseMaxShield = 0;
            bodyComponent.levelMaxShield = 0;
            bodyComponent.baseMoveSpeed = 7;
            bodyComponent.levelMoveSpeed = 0;
            bodyComponent.baseAcceleration = 80;
            bodyComponent.baseJumpPower = 15;
            bodyComponent.levelJumpPower = 0;
            bodyComponent.baseDamage = 12;
            bodyComponent.levelDamage = bodyComponent.baseDamage * 0.2f;
            bodyComponent.baseAttackSpeed = 1;
            bodyComponent.levelAttackSpeed = 0;
            bodyComponent.baseCrit = 1;
            bodyComponent.levelCrit = 0;
            bodyComponent.baseArmor = 20;
            bodyComponent.levelArmor = 0;
            bodyComponent.baseJumpCount = 1;
            bodyComponent.sprintingSpeedMultiplier = 1.45f;
            bodyComponent.wasLucky = false;
            bodyComponent.hideCrosshair = false;
            bodyComponent._defaultCrosshairPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/Crosshair/SimpleDotCrosshair");
            bodyComponent.aimOriginTransform = aimOrigin.transform;
            bodyComponent.hullClassification = HullClassification.Human;
            bodyComponent.portraitIcon = Assets.charPortrait;
            bodyComponent.isChampion = false;
            bodyComponent.currentVehicle = null;
            bodyComponent.skinIndex = 0U;
            bodyComponent.bodyColor = characterColor;

            Modules.Content.AddEntityState<DiggerMain>(out bool _);
            Modules.Content.AddEntityState<BaseEmote>(out bool _);
            Modules.Content.AddEntityState<Rest>(out bool _);
            Modules.Content.AddEntityState<Taunt>(out bool _);
            Modules.Content.AddEntityState<FallingComet>(out bool _);

            EntityStateMachine stateMachine = bodyComponent.GetComponent<EntityStateMachine>();
            stateMachine.mainStateType = new SerializableEntityStateType(typeof(DiggerMain));

            CharacterMotor characterMotor = characterBodyPrefab.GetComponent<CharacterMotor>();
            characterMotor.walkSpeedPenaltyCoefficient = 1f;
            characterMotor.characterDirection = characterDirection;
            characterMotor.muteWalkMotion = false;
            characterMotor.mass = 100f;
            characterMotor.airControl = 0.25f;
            characterMotor.disableAirControlUntilCollision = false;
            characterMotor.generateParametersOnAwake = true;

            CameraTargetParams cameraTargetParams = characterBodyPrefab.GetComponent<CameraTargetParams>();
            cameraTargetParams.cameraParams = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/MercBody").GetComponent<CameraTargetParams>().cameraParams;
            cameraTargetParams.cameraPivotTransform = null;
            //cameraTargetParams.aimMode = CameraTargetParams.AimType.Standard;
            cameraTargetParams.recoil = Vector2.zero;
            //cameraTargetParams.idealLocalCameraPos = Vector3.zero;
            cameraTargetParams.dontRaycastToPivot = false;

            ModelLocator modelLocator = characterBodyPrefab.GetComponent<ModelLocator>();
            modelLocator.modelTransform = model.transform;
            modelLocator.modelBaseTransform = modelBase.transform;

            ChildLocator childLocator = model.GetComponent<ChildLocator>();

            CharacterModel characterModel = model.AddComponent<CharacterModel>();
            characterModel.body = bodyComponent;
            characterPrefabModel = characterModel;
            characterModel.baseRendererInfos = new CharacterModel.RendererInfo[]
            {
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = model.GetComponentInChildren<SkinnedMeshRenderer>().material.SetHotpooMaterial(),
                    renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("DiamondPickL").GetComponent<MeshRenderer>().material.SetHotpooMaterial(),
                    renderer = childLocator.FindChild("DiamondPickL").GetComponent<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("DiamondPickR").GetComponent<MeshRenderer>().material.SetHotpooMaterial(),
                    renderer = childLocator.FindChild("DiamondPickR").GetComponent<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                },
                new CharacterModel.RendererInfo
                {
                    defaultMaterial = childLocator.FindChild("JokeC4").GetComponentInChildren<MeshRenderer>().material.SetHotpooMaterial(),
                    renderer = childLocator.FindChild("JokeC4").GetComponentInChildren<MeshRenderer>(),
                    defaultShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On,
                    ignoreOverlays = false
                }
            };
            characterModel.autoPopulateLightInfos = true;
            characterModel.invisibilityCount = 0;
            characterModel.temporaryOverlays = new List<TemporaryOverlay>();

            characterModel.SetFieldValue("mainSkinnedMeshRenderer", characterModel.baseRendererInfos[0].renderer.gameObject.GetComponent<SkinnedMeshRenderer>());

            TeamComponent teamComponent = null;
            if (characterBodyPrefab.GetComponent<TeamComponent>() != null) teamComponent = characterBodyPrefab.GetComponent<TeamComponent>();
            else teamComponent = characterBodyPrefab.GetComponent<TeamComponent>();
            teamComponent.hideAllyCardDisplay = false;
            teamComponent.teamIndex = TeamIndex.None;

            HealthComponent healthComponent = characterBodyPrefab.GetComponent<HealthComponent>();
            healthComponent.shield = 0f;
            healthComponent.barrier = 0f;
            healthComponent.magnetiCharge = 0f;
            healthComponent.body = null;
            healthComponent.dontShowHealthbar = false;
            healthComponent.globalDeathEventChanceCoefficient = 1f;

            characterBodyPrefab.GetComponent<Interactor>().maxInteractionDistance = 3f;
            characterBodyPrefab.GetComponent<InteractionDriver>().highlightInteractor = true;

            CharacterDeathBehavior characterDeathBehavior = characterBodyPrefab.GetComponent<CharacterDeathBehavior>();
            characterDeathBehavior.deathStateMachine = characterBodyPrefab.GetComponent<EntityStateMachine>();
            //characterDeathBehavior.deathState = new SerializableEntityStateType(typeof(GenericCharacterDeath));

            SfxLocator sfxLocator = characterBodyPrefab.GetComponent<SfxLocator>();
            //sfxLocator.deathSound = Sounds.DeathSound;
            sfxLocator.barkSound = "";
            sfxLocator.openSound = "";
            sfxLocator.landingSound = "Play_char_land";
            sfxLocator.fallDamageSound = "Play_char_land_fall_damage";
            sfxLocator.aliveLoopStart = "";
            sfxLocator.aliveLoopStop = "";

            Rigidbody rigidbody = characterBodyPrefab.GetComponent<Rigidbody>();
            rigidbody.mass = 100f;
            rigidbody.drag = 0f;
            rigidbody.angularDrag = 0f;
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.interpolation = RigidbodyInterpolation.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rigidbody.constraints = RigidbodyConstraints.None;

            CapsuleCollider capsuleCollider = characterBodyPrefab.GetComponent<CapsuleCollider>();
            capsuleCollider.isTrigger = false;
            capsuleCollider.material = null;
            capsuleCollider.center = new Vector3(0f, 0f, 0f);
            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 1.82f;
            capsuleCollider.direction = 1;

            KinematicCharacterMotor kinematicCharacterMotor = characterBodyPrefab.GetComponent<KinematicCharacterMotor>();
            kinematicCharacterMotor.CharacterController = characterMotor;
            kinematicCharacterMotor.Capsule = capsuleCollider;
            kinematicCharacterMotor.Rigidbody = rigidbody;

            kinematicCharacterMotor.DetectDiscreteCollisions = false;
            kinematicCharacterMotor.GroundDetectionExtraDistance = 0f;
            kinematicCharacterMotor.MaxStepHeight = 0.2f;
            kinematicCharacterMotor.MinRequiredStepDepth = 0.1f;
            kinematicCharacterMotor.MaxStableSlopeAngle = 55f;
            kinematicCharacterMotor.MaxStableDistanceFromLedge = 0.5f;
            kinematicCharacterMotor.PreventSnappingOnLedges = false;
            kinematicCharacterMotor.MaxStableDenivelationAngle = 55f;
            kinematicCharacterMotor.RigidbodyInteractionType = RigidbodyInteractionType.None;
            kinematicCharacterMotor.PreserveAttachedRigidbodyMomentum = true;
            kinematicCharacterMotor.HasPlanarConstraint = false;
            kinematicCharacterMotor.PlanarConstraintAxis = Vector3.up;
            kinematicCharacterMotor.StepHandling = StepHandlingMethod.None;
            kinematicCharacterMotor.LedgeHandling = true;
            kinematicCharacterMotor.InteractiveRigidbodyHandling = true;
            kinematicCharacterMotor.SafeMovement = false;

            HurtBoxGroup hurtBoxGroup = model.AddComponent<HurtBoxGroup>();

            HurtBox mainHurtbox = model.transform.Find("MainHurtbox").gameObject.AddComponent<HurtBox>();
            mainHurtbox.gameObject.layer = LayerIndex.entityPrecise.intVal;
            mainHurtbox.healthComponent = healthComponent;
            mainHurtbox.isBullseye = true;
            mainHurtbox.isSniperTarget = true;
            mainHurtbox.damageModifier = HurtBox.DamageModifier.Normal;
            mainHurtbox.hurtBoxGroup = hurtBoxGroup;
            mainHurtbox.indexInGroup = 0;

            hurtBoxGroup.hurtBoxes = new HurtBox[]
            {
                mainHurtbox
            };

            hurtBoxGroup.mainHurtBox = mainHurtbox;
            hurtBoxGroup.bullseyeCount = 1;

            HitBoxGroup hitBoxGroup = model.AddComponent<HitBoxGroup>();

            GameObject crushHitbox = new GameObject("CrushHitbox");
            crushHitbox.transform.parent = childLocator.FindChild("SwingCenter");
            crushHitbox.transform.localPosition = new Vector3(0f, 0f, 0f);
            crushHitbox.transform.localRotation = Quaternion.identity;
            crushHitbox.transform.localScale = Vector3.one * 0.08f;

            HitBox hitBox = crushHitbox.AddComponent<HitBox>();
            crushHitbox.layer = LayerIndex.projectile.intVal;

            hitBoxGroup.hitBoxes = new HitBox[]
            {
                hitBox
            };

            hitBoxGroup.groupName = "Crush";

            HitBoxGroup chargeHitBoxGroup = model.AddComponent<HitBoxGroup>();

            GameObject chargeHitbox = new GameObject("ChargeHitbox");
            chargeHitbox.transform.parent = childLocator.FindChild("SwingCenter");
            chargeHitbox.transform.localPosition = new Vector3(0f, 0f, 0f);
            chargeHitbox.transform.localRotation = Quaternion.identity;
            chargeHitbox.transform.localScale = Vector3.one * 0.05f;

            HitBox chargeHitBox = chargeHitbox.AddComponent<HitBox>();
            chargeHitbox.layer = LayerIndex.projectile.intVal;

            chargeHitBoxGroup.hitBoxes = new HitBox[]
            {
                chargeHitBox
            };

            chargeHitBoxGroup.groupName = "Charge";

            FootstepHandler footstepHandler = model.AddComponent<FootstepHandler>();
            footstepHandler.baseFootstepString = "Play_player_footstep";
            footstepHandler.sprintFootstepOverrideString = "";
            footstepHandler.enableFootstepDust = true;
            footstepHandler.footstepDustPrefab = LegacyResourcesAPI.Load<GameObject>("Prefabs/GenericFootstepDust");

            RagdollController ragdollController = model.GetComponent<RagdollController>();

            PhysicMaterial physicMat = LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterBodies/CommandoBody").GetComponentInChildren<RagdollController>().bones[1].GetComponent<Collider>().material;

            foreach (Transform i in ragdollController.bones)
            {
                if (i)
                {
                    i.gameObject.layer = LayerIndex.ragdoll.intVal;
                    Collider j = i.GetComponent<Collider>();
                    if (j)
                    {
                        j.material = physicMat;
                        j.sharedMaterial = physicMat;
                    }
                    Rigidbody k = i.GetComponent<Rigidbody>();
                    if (k) k.drag = 0.1f;
                }
            }

            AimAnimator aimAnimator = model.AddComponent<AimAnimator>();
            aimAnimator.directionComponent = characterDirection;
            aimAnimator.pitchRangeMax = 60f;
            aimAnimator.pitchRangeMin = -60f;
            aimAnimator.yawRangeMin = -90f;
            aimAnimator.yawRangeMax = 90f;
            aimAnimator.pitchGiveupRange = 30f;
            aimAnimator.yawGiveupRange = 10f;
            aimAnimator.giveupDuration = 3f;
            aimAnimator.inputBank = characterBodyPrefab.GetComponent<InputBankTest>();

            GameObject particlesObject = childLocator.FindChild("AdrenalineFire").gameObject;
            if (particlesObject)
            {
                characterBodyPrefab.AddComponent<AdrenalineParticleTimer>().init(particlesObject);
            }
        }

        private void RegisterCharacter()
        {
            characterDisplay.AddComponent<NetworkIdentity>();

            SurvivorDef survivorDef = ScriptableObject.CreateInstance<SurvivorDef>();
            (survivorDef as ScriptableObject).name = "Miner";
            survivorDef.displayNameToken = "MINER_NAME";
            survivorDef.unlockableDef = Unlockables.diggerUnlockableDef;
            survivorDef.descriptionToken = "MINER_DESCRIPTION";
            survivorDef.primaryColor = characterColor;
            survivorDef.bodyPrefab = characterBodyPrefab;
            survivorDef.displayPrefab = characterDisplay;
            survivorDef.outroFlavorToken = "MINER_OUTRO_FLAVOR";
            survivorDef.hidden = false;
            survivorDef.desiredSortPosition = 9.1f;
            survivorDef.cachedName = "Miner";

            SkillSetup();

            Modules.Content.AddBody(characterBodyPrefab);
            Modules.Content.AddSurvivorDef(survivorDef);
        }

        private void CreateDoppelganger()
        {
            doppelganger = PrefabAPI.InstantiateClone(LegacyResourcesAPI.Load<GameObject>("Prefabs/CharacterMasters/MercMonsterMaster"), "MinerMonsterMaster");
            doppelganger.GetComponent<CharacterMaster>().bodyPrefab = characterBodyPrefab;

            Modules.Content.AddMaster(doppelganger);
        }

        private void SkillSetup()
        {
            skillLocator = characterBodyPrefab.GetComponent<SkillLocator>();

            Modules.Skills.CreateSkillFamilies(characterBodyPrefab);

            PassiveSetup();

            PrimarySetup();
            SecondarySetup();
            UtilitySetup();
            SpecialSetup();

            if (ancientScepterInstalled || classicItemsInstalled)
                ScepterSkillSetup();
            if(ancientScepterInstalled)
                ScepterSetup();
            if (classicItemsInstalled)
                ScepterClassicSetup();
        }

        private void PassiveSetup()
        {
            skillLocator.passiveSkill.enabled = true;
            skillLocator.passiveSkill.skillNameToken = "MINER_PASSIVE_NAME";
            skillLocator.passiveSkill.skillDescriptionToken = "MINER_PASSIVE_DESCRIPTION";
            skillLocator.passiveSkill.icon = Assets.iconP;
        }

        private void PrimarySetup()
        {
            Modules.Content.AddEntityState<Gouge>(out bool _);

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(Gouge));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 0f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Any;
            mySkillDef.resetCooldownTimerOnUse = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.cancelSprintingOnActivation = false;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon1;
            mySkillDef.skillDescriptionToken = "MINER_PRIMARY_GOUGE_DESCRIPTION";
            mySkillDef.skillName = "MINER_PRIMARY_GOUGE_NAME";
            mySkillDef.skillNameToken = "MINER_PRIMARY_GOUGE_NAME";
            mySkillDef.keywordTokens = new string[] {
                "KEYWORD_AGILE",
                "KEYWORD_CLEAVING"
            };
            FixSkillName(mySkillDef);

            //alt
            Modules.Content.AddEntityState<Crush>(out bool _);

            SkillDef mySkillDef2 = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef2.activationState = new SerializableEntityStateType(typeof(Crush));
            mySkillDef2.activationStateMachineName = "Weapon";
            mySkillDef2.baseMaxStock = 1;
            mySkillDef2.baseRechargeInterval = 0f;
            mySkillDef2.beginSkillCooldownOnSkillEnd = false;
            mySkillDef2.canceledFromSprinting = false;
            mySkillDef2.fullRestockOnAssign = true;
            mySkillDef2.interruptPriority = InterruptPriority.Any;
            mySkillDef2.resetCooldownTimerOnUse = false;
            mySkillDef2.isCombatSkill = true;
            mySkillDef2.mustKeyPress = false;
            mySkillDef2.cancelSprintingOnActivation = false;
            mySkillDef2.rechargeStock = 1;
            mySkillDef2.requiredStock = 1;
            mySkillDef2.stockToConsume = 1;
            mySkillDef2.icon = Assets.icon1B;
            mySkillDef2.skillDescriptionToken = "MINER_PRIMARY_CRUSH_DESCRIPTION";
            mySkillDef2.skillName = "MINER_PRIMARY_CRUSH_NAME";
            mySkillDef2.skillNameToken = "MINER_PRIMARY_CRUSH_NAME";
            mySkillDef2.keywordTokens = new string[] {
                "KEYWORD_AGILE"
            };
            FixSkillName(mySkillDef2);

            Modules.Skills.AddPrimarySkills(characterBodyPrefab, mySkillDef, mySkillDef2);
            Modules.Skills.AddUnlockablesToFamily(skillLocator.primary.skillFamily, null, Unlockables.crushUnlockableDef);
        }

        private void SecondarySetup()
        {
            Modules.Content.AddEntityState<DrillChargeStart>(out bool _);
            Modules.Content.AddEntityState<DrillCharge>(out bool _);

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(DrillChargeStart));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = drillChargeCooldown.Value;
            mySkillDef.beginSkillCooldownOnSkillEnd = true;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Skill;
            mySkillDef.resetCooldownTimerOnUse = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = true;
            mySkillDef.cancelSprintingOnActivation = false;
            mySkillDef.forceSprintDuringState = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon2;
            mySkillDef.skillDescriptionToken = "MINER_SECONDARY_CHARGE_DESCRIPTION";
            mySkillDef.skillName = "MINER_SECONDARY_CHARGE_NAME";
            mySkillDef.skillNameToken = "MINER_SECONDARY_CHARGE_NAME";
            FixSkillName(mySkillDef);

            //alt
            Modules.Content.AddEntityState<DrillBreakStart>(out bool _);
            Modules.Content.AddEntityState<DrillBreak>(out bool _);

            SkillDef mySkillDef2 = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef2.activationState = new SerializableEntityStateType(typeof(DrillBreakStart));
            mySkillDef2.activationStateMachineName = "Weapon";
            mySkillDef2.baseMaxStock = 1;
            mySkillDef2.baseRechargeInterval = drillBreakCooldown.Value;
            mySkillDef2.beginSkillCooldownOnSkillEnd = true;
            mySkillDef2.canceledFromSprinting = false;
            mySkillDef2.fullRestockOnAssign = true;
            mySkillDef2.interruptPriority = InterruptPriority.Skill;
            mySkillDef2.resetCooldownTimerOnUse = false;
            mySkillDef2.isCombatSkill = true;
            mySkillDef2.mustKeyPress = true;
            mySkillDef2.cancelSprintingOnActivation = false;
            mySkillDef2.forceSprintDuringState = true;
            mySkillDef2.rechargeStock = 1;
            mySkillDef2.requiredStock = 1;
            mySkillDef2.stockToConsume = 1;
            mySkillDef2.icon = Assets.icon2B;
            mySkillDef2.skillDescriptionToken = "MINER_SECONDARY_BREAK_DESCRIPTION";
            mySkillDef2.skillName = "MINER_SECONDARY_BREAK_NAME";
            mySkillDef2.skillNameToken = "MINER_SECONDARY_BREAK_NAME";
            FixSkillName(mySkillDef2);

            Modules.Skills.AddSecondarySkills(characterBodyPrefab, mySkillDef, mySkillDef2);
            Modules.Skills.AddUnlockablesToFamily(skillLocator.secondary.skillFamily, null, Unlockables.crackHammerUnlockableDef);
        }
        private void UtilitySetup()
        {
            Modules.Content.AddEntityState<BackBlast>(out bool _);

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(BackBlast));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 5;
            mySkillDef.beginSkillCooldownOnSkillEnd = true;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.Skill;
            mySkillDef.resetCooldownTimerOnUse = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.cancelSprintingOnActivation = false;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon3;
            mySkillDef.skillDescriptionToken = "MINER_UTILITY_BACKBLAST_DESCRIPTION";
            mySkillDef.skillName = "MINER_UTILITY_BACKBLAST_NAME";
            mySkillDef.skillNameToken = "MINER_UTILITY_BACKBLAST_NAME";
            mySkillDef.keywordTokens = new string[] {
                "KEYWORD_STUNNING"
            };
            FixSkillName(mySkillDef);

            Modules.Content.AddEntityState<CaveIn>(out bool _);
            SkillDef mySkillDef2 = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef2.activationState = new SerializableEntityStateType(typeof(CaveIn));
            mySkillDef2.activationStateMachineName = "Weapon";
            mySkillDef2.baseMaxStock = 1;
            mySkillDef2.baseRechargeInterval = 5;
            mySkillDef2.beginSkillCooldownOnSkillEnd = true;
            mySkillDef2.canceledFromSprinting = false;
            mySkillDef2.fullRestockOnAssign = true;
            mySkillDef2.interruptPriority = InterruptPriority.Skill;
            mySkillDef2.resetCooldownTimerOnUse = false;
            mySkillDef2.isCombatSkill = true;
            mySkillDef2.mustKeyPress = true;
            mySkillDef2.cancelSprintingOnActivation = false;
            mySkillDef2.rechargeStock = 1;
            mySkillDef2.requiredStock = 1;
            mySkillDef2.stockToConsume = 1;
            mySkillDef2.icon = Assets.icon3B;
            mySkillDef2.skillDescriptionToken = "MINER_UTILITY_CAVEIN_DESCRIPTION";
            mySkillDef2.skillName = "MINER_UTILITY_CAVEIN_NAME";
            mySkillDef2.skillNameToken = "MINER_UTILITY_CAVEIN_NAME";
            mySkillDef2.keywordTokens = new string[] {
                "KEYWORD_STUNNING"
            };
            FixSkillName(mySkillDef2);

            Modules.Skills.AddUtilitySkills(characterBodyPrefab, mySkillDef, mySkillDef2);
            Modules.Skills.AddUnlockablesToFamily(skillLocator.utility.skillFamily, null, Unlockables.caveInUnlockableDef);
        }

        private void SpecialSetup()
        {
            Modules.Content.AddEntityState<ToTheStarsClassic>(out bool _);

            SkillDef mySkillDef2 = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef2.activationState = new SerializableEntityStateType(typeof(ToTheStarsClassic));
            mySkillDef2.activationStateMachineName = "Weapon";
            mySkillDef2.baseMaxStock = 1;
            mySkillDef2.baseRechargeInterval = 6f;
            mySkillDef2.beginSkillCooldownOnSkillEnd = false;
            mySkillDef2.canceledFromSprinting = false;
            mySkillDef2.fullRestockOnAssign = true;
            mySkillDef2.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef2.resetCooldownTimerOnUse = false;
            mySkillDef2.isCombatSkill = true;
            mySkillDef2.mustKeyPress = true;
            mySkillDef2.cancelSprintingOnActivation = true;
            mySkillDef2.rechargeStock = 1;
            mySkillDef2.requiredStock = 1;
            mySkillDef2.stockToConsume = 1;
            mySkillDef2.icon = Assets.icon4;
            mySkillDef2.skillDescriptionToken = "MINER_SPECIAL_TOTHESTARSCLASSIC_DESCRIPTION";
            mySkillDef2.skillName = "MINER_SPECIAL_TOTHESTARSCLASSIC_NAME";
            mySkillDef2.skillNameToken = "MINER_SPECIAL_TOTHESTARSCLASSIC_NAME";
            DiggerPlugin.specialClassicSkillDef = mySkillDef2;
            Modules.Skills.AddSpecialSkills(characterBodyPrefab, mySkillDef2);
            FixSkillName(mySkillDef2);

            Modules.Content.AddEntityState<ToTheStars>(out bool _);

            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(ToTheStars));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 6f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef.resetCooldownTimerOnUse = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = true;
            mySkillDef.cancelSprintingOnActivation = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon4Alt;
            mySkillDef.skillDescriptionToken = "MINER_SPECIAL_TOTHESTARS_DESCRIPTION";
            mySkillDef.skillName = "MINER_SPECIAL_TOTHESTARS_NAME";
            mySkillDef.skillNameToken = "MINER_SPECIAL_TOTHESTARS_NAME";
            DiggerPlugin.specialSkillDef = mySkillDef;
            Modules.Skills.AddSpecialSkills(characterBodyPrefab, mySkillDef);
            FixSkillName(mySkillDef);
        }

        private void ScepterSkillSetup()
        {
            Modules.Content.AddEntityState<FallingComet>(out bool _);
            SkillDef mySkillDef = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef.activationState = new SerializableEntityStateType(typeof(FallingComet));
            mySkillDef.activationStateMachineName = "Weapon";
            mySkillDef.baseMaxStock = 1;
            mySkillDef.baseRechargeInterval = 6f;
            mySkillDef.beginSkillCooldownOnSkillEnd = false;
            mySkillDef.canceledFromSprinting = false;
            mySkillDef.fullRestockOnAssign = true;
            mySkillDef.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef.resetCooldownTimerOnUse = false;
            mySkillDef.isCombatSkill = true;
            mySkillDef.mustKeyPress = false;
            mySkillDef.cancelSprintingOnActivation = true;
            mySkillDef.rechargeStock = 1;
            mySkillDef.requiredStock = 1;
            mySkillDef.stockToConsume = 1;
            mySkillDef.icon = Assets.icon4AltS;
            mySkillDef.skillDescriptionToken = "MINER_SPECIAL_SCEPTERTOTHESTARS_DESCRIPTION";
            mySkillDef.skillName = "MINER_SPECIAL_SCEPTERTOTHESTARS_NAME";
            mySkillDef.skillNameToken = "MINER_SPECIAL_SCEPTERTOTHESTARS_NAME";
            mySkillDef.keywordTokens = new string[] { "KEYWORD_IGNITE" };
            FixSkillName(mySkillDef);

            scepterSpecialSkillDef = mySkillDef;

            Modules.Content.AddEntityState<ToTheStarsClassicScepter>(out bool _);
            SkillDef mySkillDef2 = ScriptableObject.CreateInstance<SkillDef>();
            mySkillDef2.activationState = new SerializableEntityStateType(typeof(ToTheStarsClassicScepter));
            mySkillDef2.activationStateMachineName = "Weapon";
            mySkillDef2.baseMaxStock = 1;
            mySkillDef2.baseRechargeInterval = 6f;
            mySkillDef2.beginSkillCooldownOnSkillEnd = false;
            mySkillDef2.canceledFromSprinting = false;
            mySkillDef2.fullRestockOnAssign = true;
            mySkillDef2.interruptPriority = InterruptPriority.PrioritySkill;
            mySkillDef2.resetCooldownTimerOnUse = false;
            mySkillDef2.isCombatSkill = true;
            mySkillDef2.mustKeyPress = false;
            mySkillDef2.cancelSprintingOnActivation = true;
            mySkillDef2.rechargeStock = 1;
            mySkillDef2.requiredStock = 1;
            mySkillDef2.stockToConsume = 1;
            mySkillDef2.icon = Assets.icon4S;
            mySkillDef2.skillDescriptionToken = "MINER_SPECIAL_SCEPTERTOTHESTARSCLASSIC_DESCRIPTION";
            mySkillDef2.skillName = "MINER_SPECIAL_SCEPTERTOTHESTARSCLASSIC_NAME";
            mySkillDef2.skillNameToken = "MINER_SPECIAL_SCEPTERTOTHESTARSCLASSIC_NAME";
            mySkillDef2.keywordTokens = new string[] { "KEYWORD_STUNNING" };
            FixSkillName(mySkillDef2);

            scepterSpecialClassicSkillDef = mySkillDef2;
        }

        private void SetupDamageTypes()
        {
            CleaveDamage = DamageAPI.ReserveDamageType();
            On.RoR2.GlobalEventManager.OnHitEnemy += (orig, self, damageInfo, victim) =>
            {
                orig(self, damageInfo, victim);
                if (NetworkServer.active)
                {
                    if (!damageInfo.rejected && damageInfo.procCoefficient > 0f)
                    {
                        CharacterBody victimBody = victim.GetComponent<CharacterBody>();
                        if (victimBody)
                        {
                            if (damageInfo.HasModdedDamageType(CleaveDamage))
                            {
                                victimBody.AddTimedBuff(Buffs.cleaveBuff, 2.5f * damageInfo.procCoefficient);
                            }
                        }
                    }
                }
            };
            ToTheStarsClassicDamage = DamageAPI.ReserveDamageType();
            On.RoR2.HealthComponent.TakeDamage += (orig, self, damageInfo) =>
            {
                if (NetworkServer.active)
                {
                    if (damageInfo.HasModdedDamageType(ToTheStarsClassicDamage))
                    {
                        damageInfo.rejected = true;
                    }
                }
                orig(self, damageInfo);
            };

            On.RoR2.GlobalEventManager.OnHitAll += (orig, self, damageInfo, hitObject) =>
            {
                if (NetworkServer.active)
                {
                    if (damageInfo.HasModdedDamageType(ToTheStarsClassicDamage))
                    {
                        damageInfo.rejected = true;
                        damageInfo.RemoveModdedDamageType(ToTheStarsClassicDamage); //Don't let this chain.

                        //Create Explosion
                        if (damageInfo.attacker)
                        {
                            float blastRadius = 6f;
                            EffectManager.SpawnEffect(ToTheStarsClassicEffectPrefab, new EffectData
                            {
                                origin = damageInfo.position,
                                scale = blastRadius
                            }, true);
                            BlastAttack blastAttack = new BlastAttack
                            {
                                position = damageInfo.position,
                                baseDamage = damageInfo.damage,
                                baseForce = 0f,
                                radius = blastRadius,
                                attacker = damageInfo.attacker,
                                inflictor = damageInfo.attacker,
                                teamIndex = TeamComponent.GetObjectTeam(damageInfo.attacker),
                                crit = damageInfo.crit,
                                procChainMask = damageInfo.procChainMask,
                                procCoefficient = damageInfo.procCoefficient,
                                damageColorIndex = damageInfo.damageColorIndex,
                                falloffModel = BlastAttack.FalloffModel.None,
                                damageType = damageInfo.damageType,
                                bonusForce = 2000f * Vector3.down
                            };
                            blastAttack.Fire();
                        }
                    }
                }
                orig(self, damageInfo, hitObject);
            };
        }

        public static void FixSkillName(SkillDef skillDef)
        {
            (skillDef as UnityEngine.Object).name = skillDef.skillName;
        }
    }
}