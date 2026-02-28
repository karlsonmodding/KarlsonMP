using Audio;
using HarmonyLib;
using Riptide;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
            if (scene.buildIndex == 3)
            {
                if (!done)
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
                KMP_Console.Log("[Bootstrap] Initializing prefabs.. Tutorial (1/2)");
                KMP_PrefabManager.Init();
                SceneManager.LoadScene("4Escape0", LoadSceneMode.Single);
            }
            if (scene.name == "4Escape0")
            {
                KMP_Console.Log("[Bootstrap] Initializing prefabs.. Escape 0 (2/2)");
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
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            _ = instructions; // make compiler happy
            return new[] { new CodeInstruction(System.Reflection.Emit.OpCodes.Ret) };
        }
    }

    [HarmonyPatch(typeof(Debug), "Update")]
    public class Hook_Debug_Update
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // keep only this.Fps() call
            return instructions.Take(2).Append(new CodeInstruction(OpCodes.Ret));
        }
    }

    [HarmonyPatch(typeof(Timer), "Update")]
    public class Hook_Timer_Update
    {
        // for some reason the timer sometimes remains active.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            _ = instructions; // make compiler happy
            var ret = generator.DefineLabel();
            return new[]
            {
                // this.text.gameObject.activeSelf
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Timer), "text")),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Component), "get_gameObject")),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(GameObject), "get_activeSelf")),
                new CodeInstruction(OpCodes.Brfalse_S, ret), // if not active self, return
                // this.text.gameObject.SetActive(true)
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(Timer), "text")),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Component), "get_gameObject")),
                new CodeInstruction(OpCodes.Ldc_I4_0), // false
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(GameObject), "SetActive")),
                new CodeInstruction(OpCodes.Ret).WithLabels(ret)
            };
        }
    }

    [HarmonyPatch(typeof(PlayerMovement), "Update")]
    public class Hook_PlayerMovement_Update
    {
        // check for suicide even if player is in pause menu
        public static bool Prefix(PlayerMovement __instance)
        {
            if (!PlaytimeLogic.paused)
                return true;
            if (__instance.transform.position.y < -200f)
                __instance.KillPlayer();
            return false;
        }
    }
    [HarmonyPatch(typeof(PlayerMovement), "KillPlayer")]
    public class Hook_PlayerMovement_KillPlayer
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            _ = instructions; // make compiler happy
            /**
             * if(!PlaytimeLogic.suicided)
             * {
             *     ClientSend.Damage(NetworkManager.client.Id, 100); // suicide
             *     PlaytimeLogic.suicided = true;
             * }
             */
            var ret = generator.DefineLabel();
            return new[]
            {
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(PlaytimeLogic), "suicided")),
                new CodeInstruction(OpCodes.Brtrue_S, ret), // if PlaytimeLogic.suicided -> ret

                // NetworkManager.client.Id
                new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(NetworkManager), "client")),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(Client), "get_Id")),
                // 100
                new CodeInstruction(OpCodes.Ldc_I4_S, 100),
                // ClientSend.Damage(NetworkManager.client.Id, 100)
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ClientSend), "Damage")),
                // PlaytimeLogic.suicided = true
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Stsfld, AccessTools.Field(typeof(PlaytimeLogic), "suicided")),

                new CodeInstruction(OpCodes.Ret).WithLabels(ret)
            };
        }
    }

    // these functions are not complete replacements, instead they are a toggle so we keep them as is
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
        public static void MyInput(PlayerMovement __instance)
        {
            if (!Enabled) return;
            if (crouching && !__instance.crouching) // desync between crouch action and state
                __instance.StopCrouch();
            __instance.crouching = crouching;
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
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // replace base.transform.Rotate(axis, 0.5f)
            //    with base.transform.Rotate(axis, Time.deltaTime * 200)
            var codeInstructions = instructions.ToList();
            codeInstructions.RemoveAt(12); // ldc.r4 0.5
            codeInstructions.InsertRange(12, new[]
            {
                // Time.deltaTime * 200
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Time), "get_deltaTime")),
                new CodeInstruction(OpCodes.Ldc_R4, 200.0f),
                new CodeInstruction(OpCodes.Mul)
            });
            return codeInstructions;
        }
    }

    // Riptide Fix
    [HarmonyPatch(typeof(Peer), "FindMessageHandlers")]
    public class Hook_Peer_FindMessageHandlers
    {
        // we keep this as prefix since it's only called once so there is no performance gain here
        public static bool Prefix(ref MethodInfo[] __result)
        {
            __result = Assembly.GetExecutingAssembly().GetTypes().SelectMany(x => x.GetMethods()).Where(m => m.GetCustomAttributes(typeof(MessageHandlerAttribute), false).Length > 0).ToArray();
            return false;
        }
    }

    [HarmonyPatch(typeof(Milk), "OnTriggerEnter")]
    public class Hook_Milk_OnTriggerEnter
    {
        public static void fn(Milk instance, Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
            {
                var data = instance.GetComponent<KMP_PropData>();
                if (!data || !data.annouce) return;
                ClientSend.Pickup(data.id);
            }
        }
        // here we forward the call to our native function since it's easier than writing il code for it.
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            _ = instructions; // make compiler happy
            return new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Hook_Milk_OnTriggerEnter), "fn")),
                new CodeInstruction(OpCodes.Ret)
            };
        }
    }
}
