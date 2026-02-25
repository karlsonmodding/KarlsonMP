using Audio;
using HarmonyLib;
using Riptide;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using static KarlsonMP.PropManager;

namespace KarlsonMP
{
    public class Hook_Managers_Start
    {
        public static void Run()
        {
            // load tutorial 0
            SceneManager.sceneLoaded += _scene;
            UnityEngine.Object.Destroy(AudioManager.Instance);
            SceneManager.LoadScene("0Tutorial", LoadSceneMode.Single);
        }

        private static bool done = false;

        private static void _scene(Scene scene, LoadSceneMode mode)
        {
            if(scene.buildIndex == 3)
            {
                if(!done)
                {
                    KME_LevelPlayer.InitGameTex();
                    SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
                    return;
                }

                // initialize scene
                foreach (GameObject gameObject in UnityEngine.Object.FindObjectsOfType<GameObject>())
                {
                    if (gameObject.name.Contains("Enemy") || gameObject.name == "Milk" || gameObject.name == "Barrel" || gameObject.name.Contains("Boomer") || gameObject.name == "Ak47")
                    {
                        UnityEngine.Object.Destroy(gameObject);
                    }
                    if (gameObject.name == "Cube (16)")
                    {
                        gameObject.GetComponent<Rigidbody>().isKinematic = true;
                        gameObject.transform.localPosition = new Vector3(23.1498f, -5.026372f, 26.22827f);
                        gameObject.transform.localRotation = Quaternion.Euler(-30.737f, 45.003f, -270.001f);
                    }
                    if (gameObject.name == "Cube (30)")
                    {
                        gameObject.GetComponent<Rigidbody>().isKinematic = true;
                        gameObject.transform.localPosition = new Vector3(6.869867f, 21.17314f, 81.14458f);
                        gameObject.transform.localRotation = Quaternion.Euler(184.983f, -122.777f, -90.00101f);
                    }
                    if (gameObject.name == "Cube (31)")
                    {
                        gameObject.GetComponent<Rigidbody>().isKinematic = true;
                    }
                    if (gameObject.name == "Table")
                    {
                        Rigidbody[] componentsInChildren = gameObject.GetComponentsInChildren<Rigidbody>();
                        for (int j = 0; j < componentsInChildren.Length; j++)
                        {
                            componentsInChildren[j].isKinematic = true;
                        }
                    }
                }

                ClientSend.RequestScene();
                return;
            }
            if (scene.name == "MainMenu")
            {
                foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
                {
                    if (go.GetComponent<UnityEngine.UI.Button>() != null)
                    { // disable all buttons
                        go.GetComponent<UnityEngine.UI.Button>().interactable = false;
                    }
                }
                ServerBrowser.Start();

                if (!done)
                {
                    Loader.monoHooks.StartCoroutine(AudioPatch());
                    done = true;
                }
                return;
            }
            if (done) return;
            if (scene.name == "0Tutorial")
            {
                KMP_Console.Log("Initializing prefabs.. Tutorial (1/2)");
                KMP_PrefabManager.Init();
                SceneManager.LoadScene("4Escape0", LoadSceneMode.Single);
            }
            if (scene.name == "4Escape0")
            {
                KMP_Console.Log("Initializing prefabs.. Escape 0 (2/2)");
                KMP_PrefabManager.Init2();
                SceneManager.LoadScene("1Sandbox0", LoadSceneMode.Single);
            }
        }

        // Credit: https://github.com/karlsonmodding/KarlsonTAS/blob/main/Main.cs#L109 - Mang432
        private static IEnumerator AudioPatch()
        {
            yield return new WaitForSeconds(0.1f);
            AudioListener.volume = 1;
            AudioListener.pause = false;
            foreach (GameObject ga in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                Options oa = ga.GetComponent<Options>();
                if (oa != null)
                {
                    ga.SetActive(true);
                    oa.enabled = true;
                    yield return null;
                    ga.SetActive(false);
                    break;
                }
            }
            yield return new WaitForSeconds(0.1f);
            PostLoad();
        }

        private static void PostLoad()
        {
        }
    }


    [HarmonyPatch(typeof(Debug), "Fps")]
    public class Hook_Debug_Fps
    {
        public static bool Prefix(bool ___fpsOn, bool ___speedOn, TextMeshProUGUI ___fps, ref float ___deltaTime)
        {
            if (!PlayerMovement.Instance.rb)
                return false;
            if (___fpsOn || ___speedOn)
            {
                if (!___fps.gameObject.activeInHierarchy) ___fps.gameObject.SetActive(true);
                ___deltaTime += (Time.unscaledDeltaTime - ___deltaTime) * 0.1f;
                float num = ___deltaTime * 1000f;
                float num2 = 1f / ___deltaTime;
                string text = "";
                if (___fpsOn) text += string.Format("{0:0.0} ms ({1:0.} fps)", num, num2);
                if (___fpsOn && ___speedOn) text += " | ";
                if (___speedOn) text += $"m/s: {string.Format("{0:F1}", PlayerMovement.Instance.rb.velocity.magnitude)}\n";
                ___fps.text = text;
                return false;
            }
            if (___fps.gameObject.activeInHierarchy) ___fps.gameObject.SetActive(false);
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerMovement), "Pause")]
    public class Hook_PlayerMovement_Pause
    {
        public static bool Prefix() => false;
    }
    [HarmonyPatch(typeof(Debug), "OpenConsole")]
    [HarmonyPatch(typeof(Debug), "CloseConsole")]
    public class Hook_Debug_Console
    {
        public static bool Prefix() => false;
    }
    [HarmonyPatch(typeof(Debug), "Update")]
    public class Hook_Debug_Update
    {
        public static void Postfix(Debug __instance)
        {
            // for some reason the console opens even though i disabled OpenConsole and CloseConsole via Harmony
            if (__instance.console.gameObject.activeSelf)
            {
                __instance.console.gameObject.SetActive(false);
                PlayerMovement.Instance.paused = false;
            }
        }
    }

    [HarmonyPatch(typeof(Timer), "Update")]
    public class Hook_Timer_Update
    {
        // for some reason the timer sometimes remains active.
        public static void Postfix(TextMeshProUGUI ___text)
        {
            if(___text.gameObject.activeSelf)
                ___text.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(PlayerMovement), "Update")]
    public class Hook_PlayerMovement_Update
    {
        public static bool Prefix() => !PlaytimeLogic.paused;
    }
    [HarmonyPatch(typeof(PlayerMovement), "KillPlayer")]
    public class Hook_PlayerMovement_KillPlayer
    {
        public static bool Prefix()
        {
            if(!PlaytimeLogic.suicided)
            {
                ClientSend.Damage(NetworkManager.client.Id, 100); // suicide
                PlaytimeLogic.suicided = true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerMovement))]
    public static class CrouchFixes
    {
        public static bool Enabled = true;
        static bool crouching = false;
        public static void Reset()
        {
            Enabled = true;
            crouching = false;
        }
        [HarmonyPatch("StartCrouch")]
        [HarmonyPrefix]
        public static bool StartCrouch()
        {
            if (!Enabled) return true;
            if (crouching) return false;
            crouching = true;
            return true;
        }

        [HarmonyPatch("StopCrouch")]
        [HarmonyPrefix]
        public static bool StopCrouch()
        {
            if (!Enabled) return true;
            if (!crouching) return false;
            crouching = false;
            return true;
        }

        [HarmonyPatch("MyInput")]
        [HarmonyPostfix]
        public static void MyInput(ref bool ___crouching)
        {
            if (!Enabled) return;
            if (crouching && !___crouching) // desync between crouch action and state
                PlayerMovement.Instance.StopCrouch();
            ___crouching = crouching;
        }

        public static bool IsCrouching()
        {
            if (Enabled) return crouching;
            return PlayerMovement.Instance.IsCrouching();
        }
    }

    [HarmonyPatch(typeof(Milk), "Update")]
    public class Hook_Milk_Update
    {
        public static bool Prefix(Milk __instance)
        {
            // unlink milk rotation from fps
            float z = Mathf.PingPong(Time.time, 1f);
            Vector3 axis = new Vector3(1f, 1f, z);
            __instance.transform.Rotate(axis, Time.deltaTime * 200);
            return false;
        }
    }

    // Riptide Fix
    [HarmonyPatch(typeof(Peer), "FindMessageHandlers")]
    public class Hook_Peer_FindMessageHandlers
    {
        public static bool Prefix(ref MethodInfo[] __result)
        {
            __result = Assembly.GetExecutingAssembly().GetTypes().SelectMany(x => x.GetMethods()).Where(m => m.GetCustomAttributes(typeof(MessageHandlerAttribute), false).Length > 0).ToArray();
            return false;
        }
    }

    [HarmonyPatch(typeof(Milk), "OnTriggerEnter")]
    public class Hook_Milk_OnTriggerEnter
    {
        public static bool Prefix(Collider other, Milk __instance)
        {
            if(other.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                var data = __instance.GetComponent<KMP_PropData>();
                if (!data || !data.annouce) return false;
                ClientSend.Pickup(data.id);
            }
            return false;
        }
    }
}
