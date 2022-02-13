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

namespace TorchTaker
{
    [FullSerializer.fsObject("v1")]
    public class TorchTakerSaveData
    {
        public int DungeonID;
        public List<Vector3> DousedTorches;
    }

    public class TorchTaker : MonoBehaviour, IHasModSaveData
    {
        static Mod mod;
        static TorchTaker instance;
        static GameObject Torch;
        static int dungeonID;
        static List<Vector3> dousedTorches = new List<Vector3>();

        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static DaggerfallUnity dfUnity = DaggerfallUnity.Instance;

        public Type SaveDataType
        {
            get { return typeof(TorchTakerSaveData); }
        }

        public object NewSaveData()
        {
            return new TorchTakerSaveData
            {
                DungeonID = GameManager.Instance.PlayerGPS.CurrentMapID,
                DousedTorches = new List<Vector3>()
            };
        }

        public object GetSaveData()
        {
            return new TorchTakerSaveData
            {
                DungeonID = dungeonID,
                DousedTorches = dousedTorches
            };
        }

        public void RestoreSaveData(object saveData)
        {
            var torchTakerSaveData = (TorchTakerSaveData)saveData;
            dousedTorches = torchTakerSaveData.DousedTorches;
            if (dousedTorches == null)
                dousedTorches = new List<Vector3>();
            DouseTorches();
        }

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<TorchTaker>();
            instance = go.AddComponent<TorchTaker>();
            mod.SaveDataInterface = instance;

            PlayerActivate.RegisterCustomActivation(mod, 210, 16, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 210, 17, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 210, 18, TakeTorch);

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

        private static void DouseTorches()
        {           
            if (dousedTorches != null && dousedTorches.Count > 0)
            {
                RaycastHit hit;
                GameObject torch;
                foreach (Vector3 obj in dousedTorches)
                {
                    Vector3 torchUnder = obj + (Vector3.down);
                    Ray ray = new Ray(torchUnder, Vector3.up);
                    if (Physics.Raycast(ray, out hit, 10))
                    {
                        torch = hit.transform.gameObject;
                        DouseTorch(torch);
                    }
                }
                dousedTorches.Clear();
                Debug.Log("[Torch Taker] Savegame torches doused");
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
                    dousedTorches.Add(torch.transform.position);
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

        private static void DouseTorch(GameObject torch)
        {
            if (torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=16]") ||
                torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=17]") ||
                torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=18]"))
                torch.SetActive(false);
        }

        private static void OnTransitionExterior_ListCleanup(PlayerEnterExit.TransitionEventArgs args)
        {
            if(dousedTorches != null)
                dousedTorches.Clear();
        }
    }
}