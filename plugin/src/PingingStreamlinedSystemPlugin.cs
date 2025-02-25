using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FistVR;
using FMOD.Studio;
using HarmonyLib;
using UnityEngine;

namespace PingingStreamlinedSystem
{
    [BepInAutoPlugin]
    [BepInProcess("h3vr.exe")]
    public partial class PingingStreamlinedSystemPlugin : BaseUnityPlugin
    {
        internal static PingingStreamlinedSystemPlugin Instance { get; set; }

        internal Texture2D pingTexture;

        private Stack<LaserHitInfo> recentlyPointedObjects = new Stack<LaserHitInfo>(16);

        private void Awake()
        {
            Instance = this;

            StartCoroutine(LoadPingTexture(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "icon.png"));

            Options.InitialiseAndBind(Config);

            Logger = base.Logger;
            
            // Your plugin's ID, Name, and Version are available here.
            Logger.LogMessage($"{Id}: {Name} version {Version} loaded!");
        }

        private IEnumerator LoadPingTexture(string path)
        {
            if (!File.Exists(path))
                yield break;

            pingTexture = new Texture2D(256, 256, TextureFormat.DXT5, false);
            WWW www = new WWW("file://" + path);
            yield return www;
            www.LoadImageIntoTexture(pingTexture);
        }

        [HarmonyPatch(typeof(FVRViveHand), nameof(FVRViveHand.Update))]
        [HarmonyPostfix]
        private static void PatchHandUpdateToPing(FVRViveHand __instance)
        {
            bool isShootingLaser;
            if (__instance.IsInStreamlinedMode)
            {
                isShootingLaser = __instance.Input.BYButtonDown;
            }
            else
            {
                isShootingLaser = __instance.Input.TouchpadDown;
            }

            if (isShootingLaser)
            {
                Instance.ShootLaser(__instance);
            }

            if (isShootingLaser && __instance.Input.TriggerPressed)
            {
                Instance.Ping();
            }
        }

        private void ShootLaser(FVRViveHand hand)
        {
            if (Physics.Raycast(hand.Input.OneEuroPointingPos, hand.Input.OneEuroPointRotation * Vector3.forward, out var hit, Mathf.Infinity, hand.GrabLaserMask, QueryTriggerInteraction.Collide))
            {
                recentlyPointedObjects.Push(new LaserHitInfo() {
                    hitObject = hit.collider.transform,
                    hitPos = hit.point
                });
            }
        }

        private void Ping()
        {
            var lastHitInfo = recentlyPointedObjects.Pop();

            CreatePingObject(lastHitInfo.hitPos, lastHitInfo.hitObject);
        }

        private GameObject CreatePingObject(Vector3 pos, Transform parent = null)
        {
            var pingGo = new GameObject("ping");

            pingGo.AddComponent<Ping>();

            pingGo.transform.position = pos;

            pingGo.transform.parent = parent;

            return pingGo;
        }

        internal new static ManualLogSource Logger { get; private set; }

        internal static class Options
        {
            const string GENERAL = "General";

            internal static ConfigEntry<float> pingDuration;

            internal static void InitialiseAndBind(ConfigFile config)
            {
                pingDuration = config.Bind(GENERAL, "pingDuration", 10f, "The time before the ping disappears");
            }
        }
    }

    internal struct LaserHitInfo
    {
        internal Transform hitObject;
        internal Vector3 hitPos;
    }
}
