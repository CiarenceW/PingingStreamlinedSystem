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

        private static bool testing;

        internal Texture2D pingTexture;

        internal AudioEvent pingAudio;

        private Stack<LaserHitInfo> recentlyPointedObjects = new Stack<LaserHitInfo>(16);

        private void Awake()
        {
            Logger = base.Logger;

            Instance = this;

            StartCoroutine(LoadPingTexture(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/ping.png"));

            StartCoroutine(LoadPingAudio(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "/ping.ogg"));

            Options.InitialiseAndBind(Config);
            
            Harmony.CreateAndPatchAll(this.GetType());

            // Your plugin's ID, Name, and Version are available here.
            Logger.LogMessage($"{Id}: {Name} version {Version} loaded!");
        }

        private IEnumerator LoadPingAudio(string path)
        {
            Logger.LogMessage(path);

            if (!File.Exists(path))
                yield break;

            WWW www = new WWW("file://" + path);
            while (!www.isDone)
            {
                Logger.LogMessage($"Loading Ping audio: {www.progress}");
                yield return null;
            }

            pingAudio = new AudioEvent();
            pingAudio.Clips.Add(www.GetAudioClip(true, false, AudioType.OGGVORBIS));
            yield break;
        }

        private IEnumerator LoadPingTexture(string path)
        {
            Logger.LogMessage(path);

            if (!File.Exists(path))
                yield break;

            pingTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            WWW www = new WWW("file://" + path);
            while (!www.isDone)
            {
                Logger.LogMessage($"Loading Ping texture: {www.progress}");
                yield return null;
            }

            www.LoadImageIntoTexture(pingTexture);
            yield break;
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

            if (testing)
            {
                isShootingLaser = Input.GetMouseButton(2);
            }

            if (isShootingLaser)
            {
                Instance.ShootLaser(__instance);
            }

            if (isShootingLaser && (__instance.Input.TriggerPressed || Input.GetMouseButtonDown(0) && testing))
            {
                Instance.Ping();
            }
        }

        private void ShootLaser(FVRViveHand hand)
        {
            if (testing && hand.IsThisTheRightHand)
            {
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                Gizmos.color = Color.red;
                Gizmos.DrawRay(ray);

                if (Physics.Raycast(ray, out var shit, Mathf.Infinity, hand.GrabLaserMask, QueryTriggerInteraction.Collide))
                {
                    recentlyPointedObjects.Push(new LaserHitInfo()
                    {
                        hitObject = shit.collider.transform,
                        hitPos = shit.point
                    });
                }
            }
            else
            {
                if (Physics.Raycast(hand.Input.OneEuroPointingPos, hand.Input.OneEuroPointRotation * Vector3.forward, out var hit, Mathf.Infinity, hand.GrabLaserMask, QueryTriggerInteraction.Collide))
                {
                    recentlyPointedObjects.Push(new LaserHitInfo()
                    {
                        hitObject = hit.collider.transform,
                        hitPos = hit.point
                    });
                }
            }
        }

        private void Ping()
        {
            var lastHitInfo = recentlyPointedObjects.Pop();

            var pingGo = CreatePingObject(lastHitInfo.hitPos, lastHitInfo.hitObject);
            SM.PlayGenericSound(pingAudio, pingGo.transform.position);
        }

        private GameObject CreatePingObject(Vector3 pos, Transform parent = null)
        {
            var pingGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            {
                name = "ping";
            }

            pingGo.AddComponent<Ping>();

            if (parent == null)
            {
                pingGo.transform.position = pos;
            }
            else
            {
                pingGo.transform.parent = parent;
                pingGo.transform.localPosition = Vector3.zero;
            }

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
