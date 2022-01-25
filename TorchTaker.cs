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
        public List<GameObject> DeactivatedLights;
        public List<GameObject> DousedLights;
    }

    public class TorchTaker : MonoBehaviour, IHasModSaveData
    {
        static Mod mod;
        static TorchTaker instance;

        static GameObject Torch;
        static int dungeonID;
        static List<GameObject> deactivatedLights;
        static List<GameObject> dousedLights;

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
                DeactivatedLights = new List<GameObject>(),
                DousedLights = new List<GameObject>()
            };
        }

        public object GetSaveData()
        {
            Debug.Log("[Torch Taker] GetSaveData");
            return new TorchTakerSaveData
            {
                DungeonID = dungeonID,
                DeactivatedLights = deactivatedLights,
                DousedLights = dousedLights
            };
        }

        public void RestoreSaveData(object saveData)
        {
            Debug.Log("[Torch Taker] RestoreSaveData");
            var torchTakerSaveData = (TorchTakerSaveData)saveData;
            if (torchTakerSaveData.DeactivatedLights.Count > 0)
            {
                deactivatedLights = new List<GameObject>();
                SyncDeactivatedLights(torchTakerSaveData.DeactivatedLights);
            }
            if (torchTakerSaveData.DousedLights.Count > 0)
            {
                dousedLights = new List<GameObject>();
                SyncDousedLights(torchTakerSaveData.DousedLights);
            }
        }

        private static void SyncDeactivatedLights(List<GameObject> savedLights)
        {
            Debug.Log("[Torch Taker] SyncDeactivatedLights");
            if (deactivatedLights != null)
            {
                DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
                foreach (DaggerfallBillboard billBoard in lightBillboards)
                {
                    GameObject billBoardObj = billBoard.transform.gameObject;
                    GameObject savedObj = savedLights.Find(x => x.transform.position == billBoardObj.transform.position);
                    if (savedObj != null)
                    {
                        billBoardObj.SetActive(false);
                        deactivatedLights.Add(billBoardObj);
                    }
                    else
                        billBoardObj.SetActive(true);
                }
            }
            else
                Debug.Log("[Torch Taker] No deactivatedLights list");
        }

        private static void SyncDousedLights(List<GameObject> savedLights)
        {
            Debug.Log("[Torch Taker] SyncDousedLights");

            if (dousedLights != null)
            {
                DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
                foreach (DaggerfallBillboard billBoard in lightBillboards)
                {
                    GameObject billBoardObj = billBoard.transform.gameObject;
                    GameObject savedObj = savedLights.Find(x => x.transform.position == billBoardObj.transform.position);
                    if (savedObj != null)
                    {
                        savedObj.SetActive(true);
                        dousedLights.Add(savedObj);
                    }
                    else
                        Destroy(savedObj);
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

            PlayerActivate.RegisterCustomActivation(mod, 210, 6, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 210, 16, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 210, 17, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 210, 18, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 210, 20, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 210, 21, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 540, 6, LightTorch);
            PlayerActivate.RegisterCustomActivation(mod, 540, 16, LightTorch);
            PlayerActivate.RegisterCustomActivation(mod, 540, 17, LightTorch);
            PlayerActivate.RegisterCustomActivation(mod, 540, 18, LightTorch);
            PlayerActivate.RegisterCustomActivation(mod, 540, 20, LightTorch);
            PlayerActivate.RegisterCustomActivation(mod, 540, 21, LightTorch);

            PlayerEnterExit.OnTransitionExterior += OnTransitionExterior_ListCleanup;
            PlayerEnterExit.OnTransitionDungeonExterior += OnTransitionExterior_ListCleanup;
            PlayerEnterExit.OnTransitionDungeonInterior += DouseTorches;
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

            mod.IsReady = true;
        }

        private static void RemoveVanillaLightSources(PlayerEnterExit.TransitionEventArgs args)
        {
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
            DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
            foreach (DaggerfallBillboard billBoard in lightBillboards)
            {
                if (billBoard.Summary.Archive == 210)
                {
                    GameObject lightsNode = new GameObject("Lights");
                    lightsNode.transform.parent = billBoard.transform;
                    AddLight(DaggerfallUnity.Instance, billBoard.transform.gameObject, lightsNode.transform);
                }
            }
        }

        private static GameObject AddLight(DaggerfallUnity dfUnity, GameObject torch, Transform parent)
        {
            GameObject go = GameObjectHelper.InstantiatePrefab(dfUnity.Option_DungeonLightPrefab.gameObject, string.Empty, parent, torch.transform.position);
            Light light = go.GetComponent<Light>();
            if (light != null)
            {
                light.range = 5;
            }
            return go;
        }


        private static void DouseTorches(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("[Torch Taker] Dousing Torches event = " + args.ToString());
            deactivatedLights = new List<GameObject>();
            dousedLights = new List<GameObject>();
            if (HumanoidDungeon())
            {
                DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
                foreach (DaggerfallBillboard billBoard in lightBillboards)
                {
                    if (billBoard.Summary.Archive == 210)
                    {
                        DouseTorch(billBoard.transform.gameObject);
                    }
                }
            }

            Debug.Log("[Torch Taker] " + deactivatedLights.Count.ToString() + " in deactivatedTorches");
            Debug.Log("[Torch Taker] " + dousedLights.Count.ToString() + " in dousedTorches");
        }

        private static void DouseTorch(GameObject torch)
        {
            if (IsTorch(torch))
            {

                if (torch.GetComponent<DaggerfallAction>() != null)
                {
                    Debug.Log("[Torch Taker] Avoided dousing trigger");
                }
                else if (torch != null)
                {
                    if (deactivatedLights == null)
                        deactivatedLights = new List<GameObject>();
                    if (dousedLights == null)
                        dousedLights = new List<GameObject>();

                    DaggerfallBillboard torchBillboard = torch.GetComponent<DaggerfallBillboard>();
                    GameObject dousedTorch = GameObjectHelper.CreateDaggerfallBillboardGameObject(540, torchBillboard.Summary.Record, null);
                    dousedTorch.transform.position = torch.transform.position;
                    dousedTorch.SetActive(true);
                    dousedLights.Add(dousedTorch);
                    DeactivateTorch(torch);
                }
            }
        }


        private static void TakeTorch(RaycastHit hit)
        {
            PlayerActivateModes activateMode = GameManager.Instance.PlayerActivate.CurrentMode;
            if (activateMode == PlayerActivateModes.Steal)
            {
                GameObject torch = hit.transform.gameObject;
                if (torch.GetComponent<DaggerfallAction>() == null)
                {
                    DouseTorch(torch);
                    DaggerfallUnityItem TorchItem = ItemBuilder.CreateItem(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                    TorchItem.currentCondition /= UnityEngine.Random.Range(2, 4);
                    GameManager.Instance.PlayerEntity.Items.AddItem(TorchItem);
                    DaggerfallUI.AddHUDText("You take the torch.");
                }
                else
                {
                    DaggerfallUI.AddHUDText("The torch is firmly stuck...");
                }
            }
            else
            {
                DaggerfallUI.AddHUDText("You see a torch.");
            }
        }

        private static void DeactivateTorch(GameObject torch)
        {
            if (IsTorch(torch))
            {
                deactivatedLights.Add(torch);
                torch.SetActive(false);
            }
        }

        private static void LightTorch(RaycastHit hit)
        {
            PlayerActivateModes activateMode = GameManager.Instance.PlayerActivate.CurrentMode;
            if (activateMode != PlayerActivateModes.Info)
            {
                PlayerEntity playerEntity = GameManager.Instance.PlayerEntity;
                GameObject dousedTorch = hit.transform.gameObject;

                List<DaggerfallUnityItem> inventoryTorches = playerEntity.Items.SearchItems(ItemGroups.UselessItems2, (int)UselessItems2.Torch);
                bool torchAvailable = false;
                foreach (DaggerfallUnityItem torch in inventoryTorches)
                {
                    if ((playerEntity.LightSource != torch) && !torchAvailable)
                    {
                        torchAvailable = true;
                        playerEntity.Items.RemoveItem(torch);
                        DaggerfallUI.AddHUDText("You replace the torch.");
                        ActivateTorch(dousedTorch.transform.position);
                        Destroy(dousedTorch);
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

        private static void ActivateTorch(Vector3 torchPosition)
        {
            GameObject torch = new GameObject();
            Debug.Log("[Torch Taker] " + deactivatedLights.Count.ToString() + " in deactivatedTorches");
            Debug.Log("position clicked = " + torchPosition.ToString());
            foreach (GameObject listTorch in deactivatedLights)
            {
                if (listTorch.transform.position == torchPosition)
                {
                    torch = listTorch;
                    Debug.Log("torch position found in list");
                }
            }

            if (torch != null)
            {
                torch.SetActive(true);
                deactivatedLights.Remove(torch);
            }
            else
                Debug.Log("torch position not found in list");
        }


        private static bool IsTorch(GameObject torch)
        {
            if (torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=6]") ||
                torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=16]") ||
                torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=17]") ||
                torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=18]") ||
                torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=20]") ||
                torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=21]"))
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
            if (deactivatedLights != null)
                deactivatedLights.Clear();
            if (dousedLights != null)
                dousedLights.Clear();
        }
    }
}