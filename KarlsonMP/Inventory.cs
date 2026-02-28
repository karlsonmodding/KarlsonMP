using Audio;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace KarlsonMP
{
    public class Inventory
    {
        public class Weapon
        {
            public Weapon(GameObject importObject, Vector3 localScale, Vector3 meshRotation, Vector3 gunTip, Vector3 viewOffset, string soundName, float recoil, float attackSpeed, int magazine, int bulletCount, float spread, float cooldown, float boostRecoil, float maxDamage, float damageDropoff, float damageScaleByDist)
            {
                WeaponObject = WeaponLib.MakeGun(importObject.GetComponent<MeshFilter>().mesh, importObject.GetComponent<MeshRenderer>().materials, localScale, meshRotation, gunTip, recoil, attackSpeed);
                WeaponObject.SetActive(false);
                DesiredPos = viewOffset;
                SoundName = soundName;

                MaxMagazine = magazine;
                BulletCount = bulletCount;
                Spread = spread;
                Cooldown = cooldown;
                BoostRecoil = boostRecoil;
                MaxDamage = maxDamage;
                DamageDropoff = damageDropoff;
                DamageScaleByDist = damageScaleByDist;

                Magazine = MaxMagazine;
            }
            public GameObject WeaponObject;
            public readonly string SoundName;
            public readonly Vector3 DesiredPos;
            public readonly int MaxMagazine, BulletCount;
            public readonly float Spread, Cooldown, BoostRecoil, MaxDamage, DamageDropoff, DamageScaleByDist;
            public int Magazine;
            public float ReloadTime = 0f;
        }
        static List<Weapon> weapons = null;

        public static Color selfBulletColor = Color.blue;

        public static void LoadAssets(AssetBundle bundle)
        {
            deagle = bundle.LoadAsset<GameObject>("assets/karlsonmp/models/deag.fbx");
            ak47 = bundle.LoadAsset<GameObject>("assets/karlsonmp/models/ak.fbx");
            shotgun = bundle.LoadAsset<GameObject>("assets/karlsonmp/models/shotty.fbx");
        }

        private static GameObject deagle, ak47, shotgun;
        public static bool ShowDamage = true, gamerule_ShowDamage = true;
        public static void Shoot(Transform ___guntip)
        {
            if (weapons.Count == 0) return;
            if (CurrentWeaponCooldown > 0) return; // weapon switch use cooldown
            if (weapons[CurrentWeapon].ReloadTime > 0) return; // currently reloading
            if (weapons[CurrentWeapon].Magazine > 0)
            {
                // virtual shoot
                Gun.Instance.Shoot();
                KMP_AudioManager.PlaySound(weapons[CurrentWeapon].SoundName, 0.15f);
                // atm i don't know more abt this.. will need to reverse this code a bit more
                Vector3 vector = ___guntip.position - ___guntip.transform.right / 4f;
                UnityEngine.Object.Instantiate(PrefabManager.Instance.muzzle, vector, Quaternion.identity);
                Dictionary<ushort, float> damage = new Dictionary<ushort, float>();
                ushort victim;
                float dmg;
                for (int _ = 0; _ < weapons[CurrentWeapon].BulletCount; _++)
                {
                    (victim, dmg) = TraceBullet(___guntip);
                    if (dmg == 0f) continue;
                    if (!damage.ContainsKey(victim))
                        damage.Add(victim, 0f);
                    damage[victim] += dmg;
                }

                weapons[CurrentWeapon].Magazine--;

                if (damage.Count > 0)
                    Hitmarker();

                // send damage report
                foreach (var x in damage)
                {
                    ClientSend.Damage(x.Key, (int)Mathf.Floor(x.Value + 0.5f));
                    if (ShowDamage && gamerule_ShowDamage)
                        KillFeedGUI.DamageReport(((int)Mathf.Floor(x.Value + 0.5f)).ToString());
                }
            }
            if (weapons[CurrentWeapon].Magazine == 0)
            {
                weapons[CurrentWeapon].ReloadTime = MaxReloadTime;
                // 0.1s delay, to enjoy deagle sound
                KMP_AudioManager.PlaySoundDelayed("reload", 0.10f, 0.08f);
            }
        }

        public static bool EnableHitmarker = true;

        private static void Hitmarker()
        {
            KMP_AudioManager.PlaySound("hitmarker", 0.15f);
            if (!EnableHitmarker) return;
            GameObject crosshair = GameObject.Find("Managers (1)/UI/Game/Crosshair");
            GameObject hitmarker = UnityEngine.Object.Instantiate(crosshair);
            hitmarker.transform.SetParent(crosshair.transform.parent);
            hitmarker.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            hitmarker.transform.localPosition = Vector3.zero;
            hitmarker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            // original crosshair settings for hitmarker
            for (int i = 0; i < 4; ++i)
            {
                hitmarker.transform.GetChild(i).localScale = new Vector3(0.0406f, 0.015f, 0.0336f);
                if (i <= 1)
                    hitmarker.transform.GetChild(i).localPosition = new Vector3((2 * i - 1) * 4.0635f, 0f, 0f);
                else
                    hitmarker.transform.GetChild(i).localPosition = new Vector3(0f, (2 * (2 - i) + 1) * 4.0635f, 0f);
            }
            System.Collections.IEnumerator fadeHitmarker()
            {
                var imgs = hitmarker.GetComponentsInChildren<Image>();
                for (int i = 1; i <= 20; ++i)
                {
                    yield return new WaitForSeconds(0.01f);
                    foreach (var x in imgs)
                        x.color = new Color(1f, 1f, 1f, 1f - i / 20f);
                }
                UnityEngine.Object.Destroy(hitmarker);
            }
            Loader.monoHooks.StartCoroutine(fadeHitmarker());
        }

        private static (ushort, float) TraceBullet(Transform ___guntip)
        {
            ushort victim = 0;
            float damage = 0f;
            PlayerMovement.Instance.GetRb().AddForce(weapons[CurrentWeapon].WeaponObject.transform.right * weapons[CurrentWeapon].BoostRecoil, ForceMode.Impulse);
            // cast ray
            Vector3 gunTip = PlayerMovement.Instance.transform.Find("Head").position;
            // Player Collider, Ground, Object, Player Capsule Collider
            LayerMask hittable = (1 << 12) | (1 << 9) | (1 << 10) | (1 << 8);
            PlayerMovement.Instance.gameObject.layer = 13; // change layer to not hit our capsule
            Vector3 vSpread = new Vector3(UnityEngine.Random.Range(-weapons[CurrentWeapon].Spread, weapons[CurrentWeapon].Spread), UnityEngine.Random.Range(-weapons[CurrentWeapon].Spread, weapons[CurrentWeapon].Spread), UnityEngine.Random.Range(-weapons[CurrentWeapon].Spread, weapons[CurrentWeapon].Spread));
            if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward + vSpread, out RaycastHit hitInfo, 200f, hittable))
            {
                var hitPoint = hitInfo.point;
                BulletRenderer.DrawBullet(___guntip.position, hitPoint, selfBulletColor);
                ClientSend.Shoot(gunTip, hitPoint);
                if (hitInfo.transform.gameObject.GetComponent<GameObjectToPlayerId>() != null)
                {
                    float distance = hitInfo.distance;
                    if (weapons[CurrentWeapon].DamageDropoff == 0 || distance <= weapons[CurrentWeapon].DamageDropoff)
                    {
                        // calculate damage
                        damage = weapons[CurrentWeapon].MaxDamage - weapons[CurrentWeapon].DamageScaleByDist * distance;
                        if (damage < 0) damage = 0f; // dropoff not acounted by weapon specs
                        victim = hitInfo.transform.gameObject.GetComponent<GameObjectToPlayerId>().id;
                    }
                }
            }
            else
            {
                // fake bullet ray
                Vector3 hitPoint = Camera.main.transform.position + (Camera.main.transform.forward + vSpread) * 200f;
                BulletRenderer.DrawBullet(___guntip.position, hitPoint, selfBulletColor);
                ClientSend.Shoot(gunTip, hitPoint);
            }
            PlayerMovement.Instance.gameObject.layer = 8;
            return (victim, damage);
        }

        public static void Init()
        {
            weapons = new List<Weapon>();
            /*CurrentWeapon = 0;
            weapons.Add(new Weapon(ak47, new Vector3(50f, 50f, 2.5f), new Vector3(0f, 180f, 0f), new Vector3(-0.015f, -0f, 0f), Vector3.zero, "smg", 0.2f, 0.15f, 20, 1, 0.01f, 0.2f, 0, 40f, 0, 0.1f));
            weapons.Add(new Weapon(deagle, new Vector3(1.44f, 1.44f, 0.527f), new Vector3(-90f, 0f, 0f), new Vector3(-0.3f, -0.3f, 0f), new Vector3(0f, 0.2f, 0.2f), "pistol", 0.3f, 0.4f, 1, 1, 0, 0.7f, 0, 100f, 0, 0));
            weapons.Add(new Weapon(shotgun, new Vector3(40f, 50f, 2.5f), new Vector3(0f, 180f, 0f), Vector3.zero, new Vector3(0f, 0.2f, 0.2f), "shotgun", 0.5f, 1, 8, 6, 0.075f, 0.5f, 7f, 40f, 50f, 1f));

            weapons[0].WeaponObject.SetActive(true);
            PlayerMovement.Instance.ReflectionGet<DetectWeapons>("detectWeapons").ForcePickup(weapons[0].WeaponObject);*/
        }

        public static void GiveWeapon(string model, Vector3 localScale, Vector3 meshRotation, Vector3 gunTip, Vector3 viewOffset, string soundName, float recoil, float attackSpeed, int magazine, int bulletCount, float spread, float cooldown, float boostRecoil, float maxDamage, float damageDropoff, float damageScaleByDist)
        {
            GameObject importObj = null;
            if (model == "ak47")
                importObj = ak47;
            else if (model == "deagle")
                importObj = deagle;
            else if (model == "shotty")
                importObj = shotgun;
            // default game assets
            else if (model == "pistol")
                importObj = KMP_PrefabManager.pistol;
            else if (model == "uzi")
                importObj = KMP_PrefabManager.ak47;
            else if (model == "shotgun")
                importObj = KMP_PrefabManager.shotgun;
            else if (model == "grappler")
                importObj = KMP_PrefabManager.grappler;
            else if (model == "boomer")
                importObj = KMP_PrefabManager.boomer;

            // TODO: custom server assets
            if (importObj == null) return;

            weapons.Add(new Weapon(importObj, localScale, meshRotation, gunTip, viewOffset, soundName, recoil, attackSpeed, magazine, bulletCount, spread, cooldown, boostRecoil, maxDamage, damageDropoff, damageScaleByDist));
            if (weapons.Count == 1)
            {
                // init weapons
                weapons[0].WeaponObject.SetActive(true);
                PlayerMovement.Instance.detectWeapons.ForcePickup(weapons[0].WeaponObject);
                SwitchWeapon(0);
            }
        }

        public static void RemoveWeapon(int idx)
        {
            UnityEngine.Object.Destroy(weapons[idx].WeaponObject);
            weapons.RemoveAt(idx);
            if (weapons.Count > 0)
                SwitchWeapon(0);
            else
            {
                CurrentWeapon = 0;
                // de-init weapons
                PlayerMovement.Instance.detectWeapons.hasGun = false;
                PlayerMovement.Instance.detectWeapons.gun = null;
                PlayerMovement.Instance.detectWeapons.gunScript = null;
            }
        }

        public static void SwitchWeapon(int idx)
        {
            KMP_AudioManager.PlaySound("reload", 0.08f);
            Pickup pk = weapons[CurrentWeapon].WeaponObject.GetComponent<Pickup>();
            pk.StopUse();
            weapons[CurrentWeapon].WeaponObject.transform.parent = null;
            weapons[CurrentWeapon].WeaponObject.layer = LayerMask.NameToLayer("Gun");
            pk.readyToUse = true;
            pk.pickedUp = false;
            RangedWeapon rw = weapons[CurrentWeapon].WeaponObject.GetComponent<RangedWeapon>();
            rw.CancelInvoke(); // cancel all active invokes, there is a bug here, produced by our system
            weapons[CurrentWeapon].WeaponObject.SetActive(false);

            if (weapons[CurrentWeapon].ReloadTime > 0)
                weapons[CurrentWeapon].ReloadTime = 0; // we were reloading, so cancel

            CurrentWeapon = idx;
            weapons[idx].WeaponObject.SetActive(true);
            PlayerMovement.Instance.detectWeapons.ForcePickup(weapons[idx].WeaponObject);
            // somewhat nice animation
            PlayerMovement.Instance.detectWeapons.desiredPos = weapons[idx].DesiredPos;
            weapons[idx].WeaponObject.transform.localPosition = weapons[idx].DesiredPos;
            weapons[idx].WeaponObject.transform.localRotation = Quaternion.Euler(0f, 90f, 179f);
            // switch cooldown
            CurrentWeaponCooldown = weapons[idx].Cooldown;
            weapons[idx].WeaponObject.GetComponent<Pickup>().StopUse();
        }
        public static void NextWeapon()
        {
            if (weapons.Count == 0) return;
            int idx = CurrentWeapon + 1;
            if (idx >= weapons.Count) idx = 0;
            SwitchWeapon(idx);
        }
        public static void PrevWeapon()
        {
            if (weapons.Count == 0) return;
            int idx = CurrentWeapon - 1;
            if (idx < 0) idx = weapons.Count - 1;
            SwitchWeapon(idx);
        }

        public static void ReloadAll()
        {
            if (weapons == null) return;
            // remove old weapons
            foreach (var x in weapons)
                UnityEngine.Object.Destroy(x.WeaponObject);
            weapons.Clear();
            CurrentWeapon = 0;
            PlayerMovement.Instance.detectWeapons.hasGun = false;
            PlayerMovement.Instance.detectWeapons.gun = null;
            PlayerMovement.Instance.detectWeapons.gunScript = null;
        }

        // 0 - ak47, 1 - deagle, 2 - shotgun
        private static int CurrentWeapon = 0;
        private static float CurrentWeaponCooldown = 0f;

        public static bool CanShoot => CurrentWeaponCooldown == 0f && weapons[CurrentWeapon].ReloadTime == 0f;

        /*// weapon stats
        private static int[] WeaponMag = new int[] { 20, 1, 8 };
        private static float[] ReloadTime = new float[] { 0f, 0f, 0f };
        private static readonly int[]
            MaxWeaponMag = new int[] { 20, 1, 8 }, // max bullets in magazine
            BulletCount = new int[] { 1, 1, 6 }; // bullets in a shot
        private static readonly float[]
            WeaponSpread = new float[] { 0.01f, 0f, 0.075f }, // spread of a bullet
            MaxCooldownTime = new float[] { 0.2f, 0.7f, 0.5f }, // cooldown time (after reload/swtich)
            BoostRecoil = new float[] { 0f, 0f, 7f }, // boost back (shotgun effect) per bullet
            MaxDamage = new float[] { 40f, 100f, 40f }, // maximum damage the a bullet can deal (when target is at 0f distance or DamageDropoff is 0)
            DamageDropoff = new float[] { 0f, 0f, 50f }, // distance after which damage is 0
            DamageScaleByDist = new float[] { 0.1f, 0f, 1f }; // rate at which damage drops off with distance
                                                                // if dist < damagedropoff : damage = MaxDamage - DamageScaleByDist * distance
                                                                // idealy DamageDropoff should be MaxDamage / DamageScaleByDist
                                                                // but it doesn't have to be
        
        private static readonly Vector3[] DesiredPos = new Vector3[] { Vector3.zero, new Vector3(0f, 0.2f, 0.2f), new Vector3(0f, 0.2f, 0.2f) };

        private static GameObject[] WeaponObjects;
        */
        private static readonly float MaxReloadTime = 0.5f;

        public static void Update()
        {
            if (weapons == null || weapons.Count == 0) return;
            if (Input.GetKeyDown(KeyCode.R))
                if (weapons[CurrentWeapon].ReloadTime == 0 && weapons[CurrentWeapon].Magazine < weapons[CurrentWeapon].MaxMagazine) // manual reload
                {
                    weapons[CurrentWeapon].ReloadTime = MaxReloadTime;
                    KMP_AudioManager.PlaySound("reload", 0.08f);
                }

            if (CurrentWeaponCooldown != 0)
            {
                CurrentWeaponCooldown -= Time.deltaTime;
                if (CurrentWeaponCooldown <= 0f)
                    CurrentWeaponCooldown = 0f;
            }
            else
            {
                if (weapons[CurrentWeapon].ReloadTime != 0)
                {
                    weapons[CurrentWeapon].ReloadTime -= Time.deltaTime;
                    if (weapons[CurrentWeapon].ReloadTime <= 0f)
                    {
                        // finished reloading
                        weapons[CurrentWeapon].ReloadTime = 0f;
                        weapons[CurrentWeapon].Magazine = weapons[CurrentWeapon].MaxMagazine;
                        CurrentWeaponCooldown = weapons[CurrentWeapon].Cooldown;
                    }
                    else
                    {
                        // animation for reloading
                        // cubic ease-out
                        float zAngle = 360f * Mathf.Pow(weapons[CurrentWeapon].ReloadTime / MaxReloadTime, 3);
                        weapons[CurrentWeapon].WeaponObject.transform.localRotation = Quaternion.Euler(0f, 90f, zAngle);
                    }
                }
            }
        }

        private static Texture2D ammoIcon;
        public static void GuiCtor(AssetBundle bundle)
        {
            ammoIcon = bundle.LoadAsset<Texture2D>("assets/karlsonmp/ammo_icon.png");
        }

        public static void OnGUI()
        {
            if (!ClientHandle.PlayerList) return;
            if (weapons.Count == 0) return;
            if (PlaytimeLogic.spectatingId != -1) return;
            GUI.DrawTexture(new Rect(Screen.width - 100f, Screen.height - 100f, 50f, 50f), ammoIcon);
            GUI.Label(new Rect(Screen.width - 240f, Screen.height - 100f, 140f, 70f), $"<b><size=50><color=white>{weapons[CurrentWeapon].Magazine} </color></size><size=25><color=silver>/{weapons[CurrentWeapon].MaxMagazine}</color></size></b>");
        }
    }


    [HarmonyPatch(typeof(PlayerMovement), "Start")]
    public class Hook_PlayerMovement_Start
    {
        public static void Prefix(PlayerMovement __instance)
        {
            __instance.spawnWeapon = null;
        }
        public static void Postfix()
        {
            Inventory.Init();
        }
    }

    [HarmonyPatch(typeof(DetectWeapons), "Pickup")]
    public static class Hook_DetectWeapons_Pickup
    {
        public static bool Prefix()
        {
            Inventory.NextWeapon();
            return false;
        }
    }
    [HarmonyPatch(typeof(DetectWeapons), "Throw")]
    public static class Hook_DetectWeapons_Throw
    {
        public static bool Prefix()
        {
            Inventory.PrevWeapon();
            return false;
        }
    }
    [HarmonyPatch(typeof(RangedWeapon), "SpawnProjectile")]
    public class Hook_RangedWeapon_SpawnProjectile
    {
        public static bool Prefix(Transform ___guntip)
        {
            Inventory.Shoot(___guntip);
            return false;
        }
    }
    [HarmonyPatch(typeof(RangedWeapon), "Use")]
    public class Hook_RangedWeapon_Use
    {
        public static bool Prefix()
        {
            return Inventory.CanShoot;
        }
    }
}
