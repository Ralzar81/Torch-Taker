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
    public class TorchTaker : MonoBehaviour
    {
        static Mod mod;
        static TorchTaker instance;

        static GameObject Torch;
        static int dungeonID;
        static List<GameObject> deactivatedTorches;
        static List<GameObject> dousedTorches;

        static PlayerEnterExit playerEnterExit = GameManager.Instance.PlayerEnterExit;
        static DaggerfallUnity dfUnity = DaggerfallUnity.Instance;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<TorchTaker>();
            instance = go.AddComponent<TorchTaker>();

            PlayerActivate.RegisterCustomActivation(mod, 210, 16, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 210, 17, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 210, 18, TakeTorch);
            PlayerActivate.RegisterCustomActivation(mod, 540, 16, LightTorch);
            PlayerActivate.RegisterCustomActivation(mod, 540, 17, LightTorch);
            PlayerActivate.RegisterCustomActivation(mod, 540, 18, LightTorch);

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

            PlayerEnterExit.OnTransitionDungeonInterior += DouseTorches;
            deactivatedTorches = new List<GameObject>();
            dousedTorches = new List<GameObject>();
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
            DaggerfallBillboard[] lightBillboards = (DaggerfallBillboard[])FindObjectsOfType(typeof(DaggerfallBillboard)); //Get all "light emitting objects" in the dungeon
            foreach (DaggerfallBillboard billBoard in lightBillboards)
            {
                if (billBoard.Summary.Archive == 210)
                {
                    DouseTorch(billBoard.transform.gameObject);
                }
            }

            Debug.Log("[Torch Taker] " + deactivatedTorches.Count.ToString() + " in deactivatedTorches");
            Debug.Log("[Torch Taker] " + dousedTorches.Count.ToString() + " in dousedTorches");
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
                    if (deactivatedTorches == null)
                        deactivatedTorches = new List<GameObject>();
                    if (dousedTorches == null)
                        dousedTorches = new List<GameObject>();

                    GameObject dousedTorch = GameObjectHelper.CreateDaggerfallBillboardGameObject(540, 17, null);
                    dousedTorch.transform.position = torch.transform.position;
                    dousedTorch.SetActive(true);
                    dousedTorches.Add(dousedTorch);
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
                    deactivatedTorches.Add(torch);
                    DeactivateTorch(torch);
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
                deactivatedTorches.Add(torch);
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
            }
            else
            {
                DaggerfallUI.AddHUDText("You see a burned out torch.");
            }
        }

        private static void ActivateTorch(Vector3 position)
        {
            GameObject torch = new GameObject();
            Debug.Log("[Torch Taker] " + deactivatedTorches.Count.ToString() + " in deactivatedTorches");
            Debug.Log("position clicked = " + position.ToString());
            foreach (GameObject listTorch in deactivatedTorches)
            {
                Debug.Log("list position = " + listTorch.transform.position.ToString());
                if (listTorch.transform.position == position)
                {
                    torch = listTorch;
                    Debug.Log("torch position found in list");
                }
            }

            if (torch != null)
            {
                torch.SetActive(true);
                deactivatedTorches.Remove(torch);
            }
            else
                Debug.Log("torch position not found in list");
        }


        private static bool IsTorch(GameObject torch)
        {
            if (torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=16]") ||
                torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=17]") ||
                torch.name.StartsWith("DaggerfallBillboard [TEXTURE.210, Index=18]"))
                return true;
            else
                return false;
        }

        private static void OnTransitionExterior_ListCleanup(PlayerEnterExit.TransitionEventArgs args)
        {
            Debug.Log("[Torch Taker] OnTransitionExterior_ListCleanup event = " + args.ToString());
            if (deactivatedTorches != null)
                deactivatedTorches.Clear();
            if (dousedTorches != null)
                dousedTorches.Clear();
        }
    }
}