using System;
using Barotrauma;
using HarmonyLib;
using System.Reflection;

namespace AfflictionResistanceRework
{
    partial class AfflictionResistanceRework : IAssemblyPlugin
    {
        const string harmony_id = "Affliction Resistance Rework";
        public Harmony? harmonyInstance;
        public void Initialize()
        {
            harmonyInstance = new Harmony(harmony_id);
            harmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
            LuaCsLogger.Log("Affliction Resistance Rework loaded!");
        }

        public void OnLoadCompleted()
        {
        }

        public void PreInitPatching()
        {
        }

        public void Dispose()
        {
            harmonyInstance?.UnpatchSelf();
            harmonyInstance = null;
            LuaCsLogger.Log("Affliction Resistance Rework disposed!");
        }

        // 统一所有抗性计算方式为伤害倍率相乘，最后用1-最后的伤害的倍率就是最终的抗性，在实际使用抗性的地方都是1-抗性，所以最后就是倍率
        // 如果有完全减免的也可以支持，而且不会导致出现负数和超过100%的抗性
        [HarmonyPatch(typeof(Character), "GetAbilityResistance", new Type[] { typeof(Identifier) })]
        public class GetAbilityResistanceByIdentifierPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Character __instance, ref float __result, Identifier resistanceId)
            {
                float resistanceMultiplier = 1f; // 乘法初始值为1
                bool hadResistance = false;

                foreach (var (key, value) in __instance.abilityResistances)
                {
                    if (key.ResistanceIdentifier == resistanceId)
                    {
                        resistanceMultiplier *= value; // 使用乘法代替加法
                        hadResistance = true;
                    }
                }

                // NOTE: 抗性在这里是作为乘数处理的，因此 1.0 相当于 0% 的抗性。
                __result = hadResistance ? Math.Max(0, resistanceMultiplier) : 1f;

                // 跳过原方法执行
                return false;
            }
        }

        [HarmonyPatch(typeof(Character), "GetAbilityResistance", new Type[] { typeof(AfflictionPrefab) })]
        public class GetAbilityResistanceByAfflictionPatch
        {
            [HarmonyPrefix]
            public static bool Prefix(Character __instance, ref float __result, AfflictionPrefab affliction)
            {
                float resistanceMultiplier = 1f; // 乘法初始值为1
                bool hadResistance = false;

                foreach (var (key, value) in __instance.abilityResistances)
                {
                    if (key.ResistanceIdentifier == affliction.AfflictionType ||
                        key.ResistanceIdentifier == affliction.Identifier)
                    {
                        resistanceMultiplier *= value; // 使用乘法代替加法
                        hadResistance = true;
                    }
                }

                // NOTE: 抗性在这里是作为乘数处理的，因此 1.0 相当于 0% 的抗性。
                __result = hadResistance ? Math.Max(0, resistanceMultiplier) : 1f;

                // 跳过原方法执行
                return false;
            }
        }

        [HarmonyPatch(typeof(CharacterHealth), "GetResistance")]
        public class GetResistancePatch
        {
            [HarmonyPrefix]
            public static bool Prefix(CharacterHealth __instance, ref float __result, AfflictionPrefab afflictionPrefab, LimbType limbType)
            {
                // 获取伤害抗性倍率初始值
                float resistanceMultiplier = __instance.Character.GetAbilityResistance(afflictionPrefab);

                foreach (var kvp in __instance.afflictions)
                {
                    var affliction = kvp.Key;
                    // 使用乘法而不是加法，乘以伤害倍率
                    resistanceMultiplier *= 1.0f - affliction.GetResistance(afflictionPrefab.Identifier, limbType);
                }

                // 返回最终结果
                __result = 1 - resistanceMultiplier;

                // 返回 false 以跳过原始方法的执行
                return false;
            }
        }
    }
}
