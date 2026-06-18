using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TooltipValue
{
    [BepInDependency("shudnal.TradersExtended", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class TooltipValuePlugin : BaseUnityPlugin
    {
        public const string ModGUID = "Harchytek.TooltipValue";
        public const string ModName = "TooltipValue";
        public const string ModVersion = "1.0.1";

        private readonly Harmony harmony = new Harmony(ModGUID);

        private void Awake()
        {
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(ObjectDB), "Awake")]
    public static class ApplyPricesToObjectDBPatch
    {
        static void Postfix(ObjectDB __instance)
        {
            Dictionary<string, int> prices = ReadTradersExtendedJsonFiles();

            if (prices.Count == 0)
            {
                Debug.LogWarning($"[{TooltipValuePlugin.ModName}] No prices found or TradersExtended is not configured.");
                return;
            }

            int countApplied = 0;

            foreach (var priceKvp in prices)
            {
                string itemName = priceKvp.Key;
                int itemValue = priceKvp.Value;

                GameObject prefab = __instance.GetItemPrefab(itemName);
                
                if (prefab?.TryGetComponent<ItemDrop>(out var id) == true)
                {
                    id.m_itemData.m_shared.m_value = itemValue;
                    countApplied++;
                }
            }
            Debug.Log($"[{TooltipValuePlugin.ModName}] Processed {prices.Count} configured items and updates {countApplied} items that were not previously updated.");
        }

        private static Dictionary<string, int> ReadTradersExtendedJsonFiles()
        {
            Dictionary<string, int> modPrices = new Dictionary<string, int>();

            string[] possibleDirectories = {
                Paths.ConfigPath,
                Path.Combine(Paths.ConfigPath, "shudnal.TradersExtended")
            };

            foreach (string dir in possibleDirectories)
            {
                if (!Directory.Exists(dir)) continue;

                string[] files = Directory.GetFiles(dir, "*sell.json");

                foreach (string file in files)
                {
                    if (!Path.GetFileName(file).StartsWith("shudnal.TradersExtended", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        string jsonContent = File.ReadAllText(file);
                        
                        MatchCollection itemBlocks = Regex.Matches(jsonContent, @"\{[^{}]+\}");
                        
                        foreach (Match block in itemBlocks)
                        {
                            string blockText = block.Value;
                            
                            Match prefabMatch = Regex.Match(blockText, @"""prefab""\s*:\s*""([^""]+)""");
                            Match stackMatch = Regex.Match(blockText, @"""stack""\s*:\s*(\d+)");
                            Match priceMatch = Regex.Match(blockText, @"""price""\s*:\s*(\d+)");

                            if (prefabMatch.Success && priceMatch.Success)
                            {
                                string prefabName = prefabMatch.Groups[1].Value;
                                int price = int.Parse(priceMatch.Groups[1].Value);
                                
                                if (price <= 0) continue;

                                int stack = 1;
                                if (stackMatch.Success) stack = int.Parse(stackMatch.Groups[1].Value);

                                int unitPrice = price / stack;
                                if (unitPrice <= 0) unitPrice = 1;

                                if (!modPrices.ContainsKey(prefabName))
                                {
                                    modPrices.Add(prefabName, unitPrice);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[{TooltipValuePlugin.ModName}] Error reading file {file}: {ex.Message}");
                    }
                }
            }

            return modPrices;
        }
    }
}