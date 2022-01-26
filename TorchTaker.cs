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
        static bool hpmActive = false;

        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static DaggerfallUnity dfUnity = DaggerfallUnity.Instance;


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
            //enteringDungeon = false;
            var torchTakerSaveData = (TorchTakerSaveData)saveData;
            savedLightCheck = true;
            deactivatedLightsPosSaved = torchTakerSaveData.DeactivatedLightsPos;
            dousedLightsPosSaved = torchTakerSaveData.DousedLightsPos;
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

            PlayerActivate.RegisterCustomActivation(mod, 210, 6, ActivateLight);
            PlayerActivate.RegisterCustomActivation(mod, 210, 16, ActivateLight);
            PlayerActivate.RegisterCustomActivation(mod, 210, 17, ActivateLight);
            PlayerActivate.RegisterCustomActivation(mod, 210, 18, ActivateLight);
            PlayerActivate.RegisterCustomActivation(mod, 210, 20, ActivateLight);
            PlayerActivate.RegisterCustomActivation(mod, 210, 21, ActivateLight);

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
            Mod hpm = ModManager.Instance.GetMod("Handpainted Models - Main");
            if (hpm != null)
            {
                Debug.Log("[Torch Taker] HPM active");
                hpmActive = true;
            }

            PlayerEnterExit.OnTransitionDungeonInterior += SetLightCheckFlag;

            mod.IsReady = true;
        }

        void Update()
        {
            if (lightCheck && GameManager.Instance.IsPlayingGame())
            {
                lightCheck = false;
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

        private static void SetLightCheckFlag(PlayerEnterExit.TransitionEventArgs args)
        {
            lightCheck = true;
        }


        private static void DouseLights()
        {
            Debug.Log("[Torch Taker] DouseLights");
            deactivatedLightsPos = new List<Vector3>();
            dousedLightsPos = new List<Vector3>();
            //if (HumanoidDungeon())
            //{
            DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
            foreach (DaggerfallBillboard billBoard in lightBillboards)
            {
                if (billBoard.Summary.Archive == 210)
                {
                    DouseLight(billBoard.transform.gameObject);
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

            if (IsLight(lightObj))
            {
                if (dousedLightsPos.Find(x => x == lightObj.transform.position) != lightObj.transform.position)
                {
                    if (activateMode == PlayerActivateModes.Steal)
                    {
                        if (lightObj.GetComponent<DaggerfallAction>() == null)
                        {
                            DaggerfallUnityItem TorchItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                            TorchItem.currentCondition /= UnityEngine.Random.Range(2, 4);
                            playerEntity.Items.AddItem(TorchItem);
                            DouseLight(lightObj);
                            DaggerfallUI.AddHUDText("You take the torch.");
                        }
                        else
                            DaggerfallUI.AddHUDText("The torch is firmly stuck...");
                    }
                    else
                        DaggerfallUI.AddHUDText("You see a torch.");
                }
                else
                {
                    if (activateMode != PlayerActivateModes.Info)
                    {
                        List<DaggerfallUnityItem> inventoryTorches = playerEntity.Items.SearchItems(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                        bool torchAvailable = false;
                        foreach (DaggerfallUnityItem torchItem in inventoryTorches)
                        {
                            if ((playerEntity.LightSource != torchItem) && !torchAvailable)
                            {
                                torchAvailable = true;
                                playerEntity.Items.RemoveItem(torchItem);
                                DaggerfallUI.AddHUDText("You replace the torch.");
                                LightLight(lightObj);
                            }
                        }
                        if (!torchAvailable)
                            DaggerfallUI.AddHUDText("You have no unlit torches.");
                    }
                    else
                    {
                        DaggerfallUI.AddHUDText("You see a burned out torch.");
                    }
                }
            }
        }

        private static void DouseLight(GameObject lightObj)
        {
            if (IsLight(lightObj))
            {
                if (lightObj.GetComponent<DaggerfallAction>() != null)
                {
                    Debug.Log("[Torch Taker] Avoided dousing trigger");
                }
                else if (lightObj != null)
                {
                    Debug.Log("[Torch Taker] " + dousedLightsPos.Count.ToString() + " in dousedLightsPos");
                    Debug.Log("[Torch Taker] Position = " + lightObj.transform.position.ToString());
                    
                    if (hpmActive)
                    {
                        lightObj.GetComponent<AudioSource>().mute = true;
                        lightObj.transform.Find("Point light (1)").gameObject.SetActive(false);
                        lightObj.transform.Find("FlamesParticleEffect (7)").gameObject.SetActive(false);
                    }
                    else
                    {
                        DaggerfallBillboard lightBillboard = lightObj.GetComponent<DaggerfallBillboard>();
                        lightBillboard.GetComponent<AudioSource>().mute = true;
                        lightBillboard.SetMaterial(540, lightBillboard.Summary.Record);
                        if (lightBillboard.transform.Find("ImprovedDungeonLight") != null)
                            lightBillboard.transform.Find("ImprovedDungeonLight").gameObject.SetActive(false);
                        else
                            lightBillboard.transform.Find("improvedDungeonLighting").gameObject.SetActive(false);
                    }
                    dousedLightsPos.Add(lightObj.transform.position);
                    Debug.Log("[Torch Taker] " + dousedLightsPos.Count.ToString() + " in dousedLightsPos");
                }
            }
        }

        private static void LightLight(GameObject lightObj)
        {
            if (IsLight(lightObj))
            {
                Debug.Log("[Torch Taker] " + dousedLightsPos.Count.ToString() + " in dousedLightsPos");
                Debug.Log("position clicked = " + lightObj.transform.position.ToString());

                
                if (hpmActive)
                {
                    lightObj.GetComponent<AudioSource>().mute = false;
                    lightObj.transform.Find("Point light (1)").gameObject.SetActive(true);
                    lightObj.transform.Find("FlamesParticleEffect (7)").gameObject.SetActive(true);
                }
                else
                {
                    DaggerfallBillboard lightBillboard = lightObj.GetComponent<DaggerfallBillboard>();
                    lightBillboard.SetMaterial(210, lightBillboard.Summary.Record);
                    lightBillboard.GetComponent<AudioSource>().mute = false;
                    if (lightBillboard.transform.Find("ImprovedDungeonLight") != null)
                        lightBillboard.transform.Find("ImprovedDungeonLight").gameObject.SetActive(true);
                    else
                        lightBillboard.transform.Find("improvedDungeonLighting").gameObject.SetActive(true);                   
                }                
                dousedLightsPos.Remove(lightObj.transform.position);
                Debug.Log("[Torch Taker] " + dousedLightsPos.Count.ToString() + " in dousedLightsPos");
            }
        }


        private static bool IsLight(GameObject obj)
        {
            if (obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=6]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=16]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=17]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=18]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=20]") ||
                obj.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=21]"))
                return true;
            else
                return false;
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