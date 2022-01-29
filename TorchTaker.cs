// Project:         Torch Taker mod for Daggerfall Unity (http://www.dfworkshop.net)
// Copyright:       Copyright (C) 2020 Ralzar
// License:         MIT License (http://www.opensource.org/licenses/mit-license.php)
// Author:          Ralzar

using System;
using UnityEngine;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop;
using System.Collections.Generic;
using DaggerfallConnect;
using DaggerfallWorkshop.Game.Entity;

namespace TorchTaker
{
    [FullSerializer.fsObject("v1")]
    public class TorchTakerSaveData
    {
        public int DungeonID;
        public List<Vector3> DeactivatedLightsPos;
        public List<Vector3> DousedLightsPos;
    }

    public class TorchTaker : MonoBehaviour, IHasModSaveData
    {
        static Mod mod;
        static TorchTaker instance;

        static GameObject Torch;
        static int dungeonID;
        static List<Vector3> deactivatedLightsPos;
        static List<Vector3> dousedLightsPos;
        static List<Vector3> deactivatedLightsPosSaved;
        static List<Vector3> dousedLightsPosSaved;
        static bool lightCheck = false;
        static bool savedLightCheck = false;

        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static DaggerfallUnity dfUnity = DaggerfallUnity.Instance;

        static Shader LegacyShadersDiffuse = Shader.Find("Legacy Shaders/Diffuse");
        static Shader LegacyShadersVertexLit = Shader.Find("Legacy Shaders/VertexLit");

        public Type SaveDataType
        {
            get { return typeof(TorchTakerSaveData); }
        }

        public object NewSaveData()
        {
            Debug.Log("[Torch Taker] NewSaveData");
            return new TorchTakerSaveData
            {
                DungeonID = GameManager.Instance.PlayerGPS.CurrentMapID,
                DeactivatedLightsPos = new List<Vector3>(),
                DousedLightsPos = new List<Vector3>()
            };
        }

        public object GetSaveData()
        {
            Debug.Log("[Torch Taker] GetSaveData");
            return new TorchTakerSaveData
            {
                DungeonID = dungeonID,
                DeactivatedLightsPos = deactivatedLightsPos,
                DousedLightsPos = dousedLightsPos
            };
        }

        public void RestoreSaveData(object saveData)
        {
            Debug.Log("[Torch Taker] RestoreSaveData");
            var torchTakerSaveData = (TorchTakerSaveData)saveData;
            if (torchTakerSaveData.DousedLightsPos != null && torchTakerSaveData.DeactivatedLightsPos != null)
            {               
                savedLightCheck = true;
                deactivatedLightsPosSaved = torchTakerSaveData.DeactivatedLightsPos;
                dousedLightsPosSaved = torchTakerSaveData.DousedLightsPos;
            }
            Debug.Log("[Torch Taker] savedata did not contain both lists");
        }

        private static void SyncDeactivatedLights(List<Vector3> savedLights)
        {
            Debug.Log("[Torch Taker] SyncDeactivatedLights");
            deactivatedLightsPos = new List<Vector3>();
            if (deactivatedLightsPos != null)
            {
                DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
                foreach (DaggerfallBillboard billBoard in lightBillboards)
                {
                    GameObject billBoardObj = billBoard.transform.gameObject;
                    Vector3 savedPos = savedLights.Find(x => x == billBoardObj.transform.position);
                    if (savedPos == billBoardObj.transform.position)
                    {
                        billBoardObj.SetActive(false);
                        deactivatedLightsPos.Add(billBoardObj.transform.position);
                    }
                    else
                        billBoardObj.SetActive(true);
                }
            }
            else
                Debug.Log("[Torch Taker] No deactivatedLights list");
        }

        private static void SyncDousedLights(List<Vector3> savedLights)
        {
            Debug.Log("[Torch Taker] SyncDousedLights");
            dousedLightsPos = new List<Vector3>();
            if (dousedLightsPos != null)
            {
                DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
                foreach (DaggerfallBillboard billBoard in lightBillboards)
                {
                    GameObject billBoardObj = billBoard.transform.gameObject;
                    Vector3 savedPos = savedLights.Find(x => x == billBoardObj.transform.position);
                    if (savedPos == billBoardObj.transform.position)
                        DouseLight(billBoardObj);
                    else
                        LightLight(billBoardObj);
                }
            }
            else
                Debug.Log("[Torch Taker] No dousedLights list");
        }

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            instance = go.AddComponent<TorchTaker>();
            mod.SaveDataInterface = instance;

            int billboardRecord = 29;
            while (billboardRecord > -1)
            {
                if (billboardRecord != 8)
                    PlayerActivate.RegisterCustomActivation(mod, 210, billboardRecord, ActivateLight);
                billboardRecord--;
            }

            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior_ListCleanup;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionExterior_ListCleanup;
        }

        void Awake()
        {
            Mod iil = ModManager.Instance.GetMod("Improved Interior Lighting");
            if (iil != null)
            {
                Debug.Log("[Torch Taker] Improved Interior Lighting is active");
            }
            else
            {
                PlayerEnterExit.OnTransitionDungeonInterior += RemoveVanillaLightSources;
                PlayerEnterExit.OnTransitionDungeonInterior += AddVanillaLightToLightSources;
                Debug.Log("[Torch Taker] Improved Interior Lighting is not active");
            }

            PlayerEnterExit.OnTransitionDungeonInterior += SetLightCheckFlag;

            mod.IsReady = true;
        }

        void Update()
        {
            if (lightCheck && GameManager.Instance.IsPlayingGame())
            {
                lightCheck = false;
                Debug.Log("[Torch Taker] Update() lightCheck = false running DouseLights()");
                DouseLights();
            }
            if (savedLightCheck && GameManager.Instance.IsPlayingGame())
            {
                savedLightCheck = false;
                if (deactivatedLightsPosSaved.Count > 0)
                {
                    SyncDeactivatedLights(deactivatedLightsPosSaved);
                }
                if (dousedLightsPosSaved.Count > 0)
                {
                    SyncDousedLights(dousedLightsPosSaved);
                }
            }
        }

        private static void RemoveVanillaLightSources(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("[Torch Taker] RemoveVanillaLightSources");
            DungeonLightHandler[] dfLights = (DungeonLightHandler[])FindObjectsOfType(typeof(DungeonLightHandler)); //Get all dungeon lights in the scene
            for (int i = 0; i < dfLights.Length; i++)
            {
                if (dfLights[i].gameObject.name.StartsWith("DaggerfallLight [Dungeon]"))
                {
                    Destroy(dfLights[i].gameObject);
                }
            }
        }

        private static void AddVanillaLightToLightSources(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("[Torch Taker] AddVanillaLightToLightSources");
            DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
            foreach (DaggerfallBillboard billBoard in lightBillboards)
            {
                if (billBoard.Summary.Archive == 210)
                {
                    GameObject lightsNode = new GameObject("ImprovedDungeonLight");
                    lightsNode.transform.SetParent(billBoard.transform);

                    lightsNode.transform.localPosition = new Vector3(0, 0, 0);
                    Light newLight = lightsNode.AddComponent<Light>();
                    newLight.range = 6;
                }
            }
        }

        private static void AddTrigger(GameObject obj)
        {
            BoxCollider boxTrigger = obj.AddComponent<BoxCollider>();
            {
                boxTrigger.isTrigger = true;
            }
        }

        private static void SetLightCheckFlag(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("[Torch Taker] SetLightCheckFlag() lightCheck = true");
            lightCheck = true;
        }


        private static void DouseLights()
        {
            Debug.Log("[Torch Taker] DouseLights");
            deactivatedLightsPos = new List<Vector3>();
            dousedLightsPos = new List<Vector3>();
            //if (HumanoidDungeon())
            //{
            //DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
            GameObject[] lightObjects = (GameObject[])FindObjectsOfType(typeof(GameObject));
            Debug.Log("[Torch Taker] lightObjects.Length = " + lightObjects.Length.ToString());
            foreach (GameObject obj in lightObjects)
            {
                Debug.Log("[Torch Taker] obj.name = " + obj.name.ToString());
                if (obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210,"))
                {
                    DouseLight(obj);
                }
            }
            //}

            Debug.Log("[Torch Taker] " + deactivatedLightsPos.Count.ToString() + " in deactivatedTorches");
            Debug.Log("[Torch Taker] " + dousedLightsPos.Count.ToString() + " in dousedTorches");
        }




        private static void ActivateLight(RaycastHit hit)
        {
            PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
            PlayerActivateModes activateMode = GameManager.Instance.PlayerActivate.CurrentMode;
            GameObject lightObj = hit.transform.gameObject;
            int lightType = LightTypeInt(lightObj);
            string itemName = lightNameFromInt(lightType);
            if (lightType > 0)
            {
                if (dousedLightsPos.Find(x => x == lightObj.transform.position) != lightObj.transform.position)
                {
                    if (lightType == 1) //torch
                    {
                        if (activateMode == PlayerActivateModes.Steal)
                        {
                            if (lightObj.GetComponent<DaggerfallAction>() == null)
                            {
                                DaggerfallUnityItem TorchItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                                TorchItem.currentCondition /= UnityEngine.Random.Range(2, 4);
                                playerEntity.Items.AddItem(TorchItem);
                                DouseLight(lightObj);
                                DaggerfallUI.AddHUDText("You take the " + itemName + ".");
                            }
                            else
                                DaggerfallUI.AddHUDText("The " + itemName + " is firmly stuck...");
                        }
                        else
                        {
                            DaggerfallUI.AddHUDText("You see a " + itemName + ".");
                        }
                    }
                    else if (lightType == 2) //candle
                    {
                        if (activateMode == PlayerActivateModes.Steal)
                        {
                            if (lightObj.GetComponent<DaggerfallAction>() == null)
                            {
                                DouseLight(lightObj);
                                DaggerfallUI.AddHUDText("You extinguish the " + itemName + ".");
                            }
                            else
                                DaggerfallUI.AddHUDText("The " + itemName + " is firmly stuck...");
                        }
                        else if (activateMode == PlayerActivateModes.Info)
                        {
                            DaggerfallUI.AddHUDText("You see a " + itemName + ".");
                        }
                    }
                    else if (lightType == 3) //lantern
                    {
                        if (activateMode == PlayerActivateModes.Steal)
                        {
                            if (lightObj.GetComponent<DaggerfallAction>() == null)
                            {
                                DaggerfallUnityItem oilItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2, (int)UselessItems2.Oil);
                                playerEntity.Items.AddItem(oilItem);
                                DouseLight(lightObj);
                                DaggerfallUI.AddHUDText("You pour the oil out of the " + itemName + ".");
                            }
                            else
                                DaggerfallUI.AddHUDText("The " + itemName + " is firmly stuck...");
                        }
                        else if (activateMode == PlayerActivateModes.Info)
                        {
                            DaggerfallUI.AddHUDText("You see a " + itemName + ".");
                        }
                    }
                    else if (lightType >= 4) //brazier or campfire
                    {
                        DaggerfallUI.AddHUDText("You see a burning " + itemName + ".");
                        DouseLight(lightObj);
                    }

                }
                else
                {
                    if (activateMode != PlayerActivateModes.Info)
                    {
                        bool fuelAvailable = false;
                        if (lightType == 1) //torch
                        {
                            List<DaggerfallUnityItem> inventoryTorches = playerEntity.Items.SearchItems(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                            foreach (DaggerfallUnityItem torchItem in inventoryTorches)
                            {
                                if ((playerEntity.LightSource != torchItem) && !fuelAvailable)
                                {
                                    fuelAvailable = true;
                                    playerEntity.Items.RemoveItem(torchItem);
                                    DaggerfallUI.AddHUDText("You replace the torch.");
                                    LightLight(lightObj);
                                }
                            }
                            if (!fuelAvailable)
                                DaggerfallUI.AddHUDText("You have no unlit torches.");
                        }
                        else if (lightType == 2) //candle
                        {
                            DaggerfallUI.AddHUDText("You light the candle.");
                            LightLight(lightObj);
                        }
                        else if (lightType == 3) //lantern
                        {
                            List<DaggerfallUnityItem> inventoryOil = playerEntity.Items.SearchItems(ItemGroups.UselessItems2, (int)UselessItems2.Oil);
                            foreach (DaggerfallUnityItem oilItem in inventoryOil)
                            {
                                if ((playerEntity.LightSource != oilItem) && !fuelAvailable)
                                {
                                    fuelAvailable = true;
                                    oilItem.stackCount -= 1;
                                    if (oilItem.stackCount <= 0)
                                        playerEntity.Items.RemoveItem(oilItem);
                                    DaggerfallUI.AddHUDText("You refill the " + itemName + ".");
                                    LightLight(lightObj);
                                }
                            }
                            if (!fuelAvailable)
                                DaggerfallUI.AddHUDText("You have no oil.");
                        }
                        else if (lightType >= 4) //brazier or campfire
                        {
                            DaggerfallUI.AddHUDText("You light the " + itemName + ".");
                            LightLight(lightObj);
                        }
                        
                    }
                    else
                    {
                        DaggerfallUI.AddHUDText("You see an extinguished " + itemName + ".");
                    }
                }
            }
        }

        private static void DouseLight(GameObject lightObj)
        {
            Debug.Log("[Torch Taker] DouseLight() " + lightObj.name);
            int lightTypeInt = LightTypeInt(lightObj);
            if (lightTypeInt > 0)
            {
                if (lightObj.GetComponent<DaggerfallAction>() != null)
                {
                    Debug.Log("[Torch Taker] Avoided dousing trigger");
                }
                else if (lightObj != null)
                {
                    Debug.Log("[Torch Taker] " + dousedLightsPos.Count.ToString() + " in dousedLightsPos");
                    Debug.Log("[Torch Taker] Position = " + lightObj.transform.position.ToString());
                    
                    DaggerfallBillboard lightBillboard = lightObj.GetComponent<DaggerfallBillboard>();
                    if (lightBillboard != null)
                        lightBillboard.SetMaterial(540, lightBillboard.Summary.Record);

                    ParticleSystem[] lightParticles = lightObj.GetComponentsInChildren<ParticleSystem>(true);
                    foreach (ParticleSystem lightParticle in lightParticles)
                    {
                        if (lightParticle != null)
                            lightParticle.transform.gameObject.SetActive(false);
                    }

                    Light[] lightLights = lightObj.GetComponentsInChildren<Light>(true);
                    foreach (Light lightLight in lightLights)
                    {
                        if (lightLight != null)
                            lightLight.transform.gameObject.SetActive(false);
                    }

                    MeshRenderer[] lightMeshs = lightObj.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (MeshRenderer lightMesh in lightMeshs)
                    {
                        if (lightMesh != null)
                        {
                            Renderer lightMeshRend = lightMesh.transform.gameObject.GetComponent<Renderer>();
                                if (lightBillboard == null && lightMeshRend != null && lightMeshRend.material.shader != null && lightTypeInt == 1)
                                    lightMeshRend.material.shader = Shader.Find("Legacy Shaders/Diffuse");
                        }
                    }

                    Renderer lightRend = lightObj.GetComponent<Renderer>();
                    if (lightBillboard == null && lightRend != null && lightRend.material.shader != null && lightTypeInt == 1)
                        lightRend.material.shader = Shader.Find("Legacy Shaders/Diffuse");


                    if (lightObj.GetComponent<AudioSource>() != null)
                        lightObj.GetComponent<AudioSource>().mute = true;

                    if (lightObj.GetComponent<BoxCollider>() == null)
                        AddTrigger(lightObj);

                    dousedLightsPos.Add(lightObj.transform.position);
                    Debug.Log("[Torch Taker] " + dousedLightsPos.Count.ToString() + " in dousedLightsPos");
                }
            }
        }

        private static void LightLight(GameObject lightObj)
        {
            int lightTypeInt = LightTypeInt(lightObj);
            if (lightTypeInt > 0)
            {
                Debug.Log("position clicked = " + lightObj.transform.position.ToString());

                DaggerfallBillboard lightBillboard = lightObj.GetComponent<DaggerfallBillboard>();
                if (lightBillboard != null)
                    lightBillboard.SetMaterial(210, lightBillboard.Summary.Record);

                ParticleSystem[] lightParticles = lightObj.GetComponentsInChildren<ParticleSystem>(true);
                foreach (ParticleSystem lightParticle in lightParticles)
                {
                    if (lightParticle != null)
                        lightParticle.transform.gameObject.SetActive(true);
                }

                Light[] lightLights = lightObj.GetComponentsInChildren<Light>(true);
                foreach (Light lightLight in lightLights)
                {
                    if (lightLight != null)
                        lightLight.transform.gameObject.SetActive(true);
                }

                MeshRenderer[] lightMeshs = lightObj.GetComponentsInChildren<MeshRenderer>(true);
                foreach (MeshRenderer lightMesh in lightMeshs)
                {
                    if (lightMesh != null)
                    {
                        Renderer lightMeshRend = lightMesh.transform.gameObject.GetComponent<Renderer>();
                        if (lightBillboard == null && lightMeshRend != null && lightMeshRend.material.shader != null && lightTypeInt == 1)
                            lightMeshRend.material.shader = Shader.Find("Legacy Shaders/VertexLit");
                    }
                }

                Renderer lightRend = lightObj.GetComponent<Renderer>();                    
                if (lightBillboard == null && lightRend != null && lightRend.material.shader != null && lightTypeInt == 1)
                    lightObj.GetComponent<Renderer>().material.shader = Shader.Find("Legacy Shaders/VertexLit");

                if (lightObj.GetComponent<AudioSource>() != null)
                    lightObj.GetComponent<AudioSource>().mute = false;                
               
                dousedLightsPos.Remove(lightObj.transform.position);
                Debug.Log("[Torch Taker] " + dousedLightsPos.Count.ToString() + " in dousedLightsPos");
            }
        }


        private static int LightTypeInt(GameObject obj)
        {
            //0 = not light item
            //1 = torch
            //2 = candle
            //3 = lamp or lantern
            //4 = brazier
            //5 = campfire
            if (obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=6]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=16]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=17]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=18]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=20]"))
                return 1;
            else if (
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=2]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=3]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=4]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=5]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=7]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=9]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=10]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=11]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=19]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=21]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=23]"))
                return 2;
            else if (
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=12]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=13]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=14]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=15]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=22]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=24]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=25]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=26]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=27]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=28]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=29]"))
                return 3;
            else if (
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=0]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=19]")
                )
                return 4;
            else if (obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=1]")
                )
                return 5;
            else
                return 0;
        }

        private static string lightNameFromInt(int identifier)
        {
            switch (identifier)
            {
                case 1:
                    return "torch";
                case 2:
                    return "candle";
                case 3:
                    return "lamp";
                case 4:
                    return "brazier";
                case 5:
                    return "campfire";
                default:
                    return "light";

            }
        }

        private static bool HumanoidDungeon()
        {
            switch (GameManager.Instance.PlayerGPS.CurrentLocation.MapTableData.DungeonType)
            {
                case DFRegion.DungeonTypes.BarbarianStronghold:
                case DFRegion.DungeonTypes.Coven:
                case DFRegion.DungeonTypes.HumanStronghold:
                case DFRegion.DungeonTypes.Laboratory:
                case DFRegion.DungeonTypes.OrcStronghold:
                case DFRegion.DungeonTypes.Prison:
                    return true;
            }

            return false;
        }

        private static void OnTransitionExterior_ListCleanup(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("[Torch Taker] OnTransitionExterior_ListCleanup event = " + args.ToString());
            if (deactivatedLightsPos != null)
                deactivatedLightsPos.Clear();
            if (dousedLightsPos != null)
                dousedLightsPos.Clear();
        }
    }
}