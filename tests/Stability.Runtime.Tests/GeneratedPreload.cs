// Generated from actual ClientPatch.cs; do not hand edit.
using System;using System.Collections;using System.Collections.Generic;using HarmonyLib;using UnityEngine;using UnityEngine.ResourceManagement.AsyncOperations;
namespace TownOfHost {
internal static class PreloadBodyHash {internal const string Value="DFA542B42399BEFE62B539C812A56DA5AE7AAF46351DCEAFEA7D6B9A226E06B3";}
    [HarmonyPatch(typeof(AmongUsClient))]
    class PreloadMapPatch
    {
        public static bool lastToggle = false;
        public static List<AsyncOperationHandle> handles = new();
        public static Dictionary<byte, ShipStatus> ships = new();

        [HarmonyPostfix]
        [HarmonyPatch(nameof(AmongUsClient.Awake))]
        [HarmonyPatch(nameof(AmongUsClient.OnGameJoined))]
        public static void Preload()
        {
            if (lastToggle == Main.PreloadMapAssets.Value) return;
            lastToggle = Main.PreloadMapAssets.Value;

            if (lastToggle)
            {
                AmongUsClient.Instance.StartCoroutine(CoLoadAssets().WrapToIl2Cpp());
            }
            else
            {
                foreach (var handle in handles)
                {
                    if (handle.IsValid())
                    {
                        // Unloading must not dereference a released or still-pending result.
                        Logger.Warn("Start Unload cached map", nameof(PreloadMapPatch));
                        handle.Release();
                    }
                }
                ships.Clear();
                handles.Clear();
            }
        }

        public static IEnumerator CoLoadAssets()
        {
            var maps = EnumHelper.GetAllValues<MapNames>();
            var shipPrefabs = AmongUsClient.Instance.ShipPrefabs;
            for (var i = 0; i < maps.Length; ++i)
            {
                var mapId = maps[i];
                var ship = shipPrefabs[i];
                if (lastToggle == false) break; //途中でOFFになった場合は中断
                yield return CoLoad(ship, mapId);
            }
        }

        public static IEnumerator CoLoad(UnityEngine.AddressableAssets.AssetReference ship, MapNames mapId)
        {
            AsyncOperationHandle handle;

            if (ship.OperationHandle.IsValid() && ship.OperationHandle.Status == AsyncOperationStatus.Succeeded)
            {
                Logger.Warn($"Skip Load {mapId}", nameof(PreloadMapPatch));
                handle = ship.OperationHandle;
            }
            else
            {
                Logger.Warn($"Start Load {mapId}", nameof(PreloadMapPatch));
                handle = ship.LoadAssetAsync<GameObject>();
                handles.Add(handle);

                yield return handle;
            }

            Logger.Warn($"End Load {mapId}", nameof(PreloadMapPatch));

            if (handle.IsValid() && handle.Status == AsyncOperationStatus.Succeeded)
            {
                var obj = handle.Result.Cast<GameObject>();
                ships[(byte)mapId] = obj.GetComponent<ShipStatus>();
            }
            else
            {
                Logger.Error($"Failed to load {mapId}", nameof(PreloadMapPatch));
            }
        }
    }
}
