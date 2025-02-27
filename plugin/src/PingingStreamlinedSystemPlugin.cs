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
using H3MP;
using HarmonyLib;
using UnityEngine;
using FFmpeg.AutoGen;
using System.Linq;
using H3MP.Networking;

namespace PingingStreamlinedSystem
{
    [BepInAutoPlugin]
    [BepInProcess("h3vr.exe")]
    [BepInDependency("VIP.TommySoucy.H3MP", BepInDependency.DependencyFlags.SoftDependency)] //this is soft for testing, whatever, blabla I'm crazy now
    public partial class PingingStreamlinedSystemPlugin : BaseUnityPlugin
    {
        public const string PING_PACKET_ID = "PiSS-Ping";

        internal static PingingStreamlinedSystemPlugin Instance { get; set; }

        private static bool testing; //so I don't have to plug in my stupid chud headset everytime I want to test the most minute feature

        private static bool networkingSetUp;

        internal Material pingMat = new Material(Shader.Find("Alloy/Unlit"));

        internal static bool isH3MPInstalled = BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("VIP.TommySoucy.H3MP", out var _);

        internal Texture2D pingTexture;

        internal AudioEvent pingAudio;

        private Stack<LaserHitInfo> recentlyPointedObjects = new Stack<LaserHitInfo>(16);

        private void Awake()
        {
            Logger = base.Logger;

            Instance = this;

            StartCoroutine(LoadPingTexture(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\ping.png"));

            StartCoroutine(LoadPingAudio(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\ping.ogg")); //fucking bullshit has to be a backward slash? fucking windows fucking fuck motherfuckf ufkc

            if (isH3MPInstalled)
            {
                ConfigureH3MPStuff();
            }

            Options.InitialiseAndBind(Config);
            
            Harmony.CreateAndPatchAll(this.GetType());

            // Your plugin's ID, Name, and Version are available here.
            Logger.LogMessage($"{Id}: {Name} version {Version} loaded!");
        }

        private int GetHostCustomPacket(string id)
        {
            return Mod.registeredCustomPacketIDs.ContainsKey(id) ? Mod.registeredCustomPacketIDs[id] : Server.RegisterCustomPacketType(id);
        }

        private bool IsClientCustomPacketRegistered(string id)
        {
            return Mod.registeredCustomPacketIDs.ContainsKey(id);
        }

        private void ConfigureH3MPStuff()
        {
            if (Mod.managerObject == null)
                return;

            //this is not how you do this
            if (ThreadManager.host)
            {
                int pingId = GetHostCustomPacket(PING_PACKET_ID);
                Mod.customPacketHandlers[pingId] = HandlePingPacket;
            }
            else
            {
                if (IsClientCustomPacketRegistered(PING_PACKET_ID))
                {
                    int pingId = Mod.registeredCustomPacketIDs[PING_PACKET_ID];
                    Mod.customPacketHandlers[pingId] = HandlePingPacket;
                }
                else
                {
                    ClientSend.RegisterCustomPacketType(PING_PACKET_ID);
                    Mod.CustomPacketHandlerReceived += ReceivedPingPacket;
                }
            }

            networkingSetUp = true;
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

            if (isShootingLaser && (__instance.Input.TriggerPressed || Input.GetMouseButtonDown(0) && testing && __instance.IsThisTheRightHand))
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
            SM.PlayGenericSound(pingAudio, GM.CurrentPlayerBody.Head.transform.position);

            if (isH3MPInstalled)
            {
                if (!networkingSetUp)
                {
                    ConfigureH3MPStuff();
                }
            }
        }

        private void ReceivedPingPacket(string id, int index)
        {

        }

        private void HandlePingPacket(int id, Packet packet)
        {

        }

        private GameObject CreatePingObject(Vector3 pos, Transform parent = null)
        {
            var pingGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            {
                name = "ping";
            }

            pingGo.AddComponent<Ping>();

            if (parent == null && parent.GetComponent<MeshRenderer>() != null && !parent.GetComponent<MeshRenderer>().isPartOfStaticBatch)
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
