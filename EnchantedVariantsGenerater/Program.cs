using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DynamicData;

namespace EnchantedVariantsGenerater
{
    public class EnchantmentInfo
    {
        public IObjectEffectGetter? Enchantment { get; set; }
        public string? EditorID { get; internal set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string? Sublist { get; set; }
        public short Level { get; set; }
        public ushort? EnchantmentAmount { get; set; }
        public bool Boss { get; set; }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public class Program
    {
        public static LeveledItemEntry CreateLeveledItemEntry(short level, FormKey reference)
        {
            var leveledItemEntry = new LeveledItemEntry
            {
                Data = new LeveledItemEntryData()
                {
                    Count = 1,
                    Level = level,
                }
            };
            leveledItemEntry.Data.Reference.SetTo(reference);
            return leveledItemEntry;
        }

        public static LeveledItemEntry CreateLeveledItemEntry(short level, LeveledItem reference)
        {
            var leveledItemEntry = new LeveledItemEntry
            {
                Data = new LeveledItemEntryData()
                {
                    Count = 1,
                    Level = level,
                }
            };
            leveledItemEntry.Data.Reference.SetTo(reference);
            return leveledItemEntry;
        }

        public static EnchantmentInfo ParseEnchantments(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Dictionary<String, IObjectEffectGetter> enchantments_cache, EnchantmentJSON enchantmentJSON)
        {

            IObjectEffectGetter enchantment;

            if (enchantmentJSON.EditorID != null)
            {
                if (enchantments_cache.ContainsKey(enchantmentJSON.EditorID))
                {
                    enchantment = enchantments_cache[enchantmentJSON.EditorID];
                } else
                {
                    enchantment = state.LinkCache.Resolve<IObjectEffectGetter>(enchantmentJSON.EditorID);
                    enchantments_cache.Add(enchantmentJSON.EditorID, enchantment);
                }
            }
            else
            {
                throw new Exception("ERROR: enchantment does not have a editorID specified");
            }
            var enchantmentGetter = new EnchantmentInfo
            {
                EnchantmentAmount = (ushort?)enchantmentJSON.EnchantmentAmount,
                Enchantment = enchantment,
                Prefix = enchantmentJSON.Prefix,
                Suffix = enchantmentJSON.Suffix,
                EditorID = enchantment.EditorID,
            };

            return enchantmentGetter;
        }

        public static LeveledItem CreateLItemEnch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, IMajorRecordGetter referenceGetter, string prefix, string suffix)
        {
            LeveledItem LItemEnch;

            string LItemEnch_EditorID = prefix + referenceGetter.EditorID + suffix;
            if (state.LinkCache.TryResolve<ILeveledItemGetter>(LItemEnch_EditorID, out var LItemEnch_Original))
            {
                Console.WriteLine("Leveled List \"" + LItemEnch_EditorID + "\" already exists in plugin \"" + LItemEnch_Original.FormKey.ModKey.ToString() + "\", copying as override and appending changes");
                LItemEnch = LItemEnch_Original.DeepCopy();
                state.PatchMod.LeveledItems.Set(LItemEnch);
            }
            else
            {
                LItemEnch = new LeveledItem(state.PatchMod);
                LItemEnch.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
                LItemEnch.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                LItemEnch.EditorID = LItemEnch_EditorID;
                LItemEnch.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
            }
            return LItemEnch;
        }

        public static LeveledItem CreateLItemEnch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, string LItemEnch_EditorID)
        {
            LeveledItem LItemEnch;

            if (state.LinkCache.TryResolve<ILeveledItemGetter>(LItemEnch_EditorID, out var LItemEnch_Original))
            {
                Console.WriteLine("Leveled List \"" + LItemEnch_EditorID + "\" already exists in plugin \"" + LItemEnch_Original.FormKey.ModKey.ToString() + "\", copying as override and appending changes");
                LItemEnch = LItemEnch_Original.DeepCopy();
                state.PatchMod.LeveledItems.Set(LItemEnch);
            }
            else
            {
                LItemEnch = new LeveledItem(state.PatchMod);
                LItemEnch.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
                LItemEnch.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                LItemEnch.EditorID = LItemEnch_EditorID;
                LItemEnch.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
            }
            return LItemEnch;
        }

        public static Config GetConfig(String path)
        {
            Config? config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(path));
            if (config == null) throw new Exception();

            return config;
        }

        /*
        public static void CopyVirtualMachineAdapter(VirtualMachineAdapter? copyTo, IVirtualMachineAdapterGetter copyFrom)
        {
            if (copyFrom.TryGet(out var output))
            {
                copyTo.CopyInFromBinary(copyFrom.WriteToBinary(MutagenWriter.));
            } else
            {
                throw new Exception("");
            }
        }
        */

        // state.LinkCache.TryResolve<IWeaponGetter>(enchanted_weapon_EditorID, out var Origenchanted_weapon

        public static Noggog.ExtendedList<LeveledItemEntry> GetLeveledItemEntries(LeveledItem leveledItem)
        {
            Noggog.ExtendedList<LeveledItemEntry>? leveledItemEntries = leveledItem.Entries;
            if (leveledItemEntries == null) leveledItemEntries = new();
            return leveledItemEntries;
        }

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "enchantments.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            // Read JSON Files
            List<InputWeaponsJSON> inputWeaponsJSON = new();
            List<InputArmorsJSON> inputArmorsJSONs = new();

            Config config = GetConfig(Path.Combine(state.ExtraSettingsDataPath, "config.json"));


            foreach (var filePath in Directory.GetFiles(state.ExtraSettingsDataPath + "\\input", "*.json"))
            {
                Console.WriteLine("Reading JSON file \"" + filePath + "\"");
                var inputJSON = JsonConvert.DeserializeObject<InputFileJSON>(File.ReadAllText(filePath));
                if (inputJSON == null) continue;
                if (inputJSON.InputWeapons != null) inputWeaponsJSON.Add(inputJSON.InputWeapons);
                if (inputJSON.InputArmors != null) inputArmorsJSONs.Add(inputJSON.InputArmors);
            }

            Dictionary<String, IObjectEffectGetter> weapon_enchantments_cache = new();
            Dictionary<String, IObjectEffectGetter> armor_enchantments_cache = new();
            Dictionary<String, IWeaponGetter> weapons_cache = new();
            Dictionary<String, IArmorGetter> armor_cache = new();

            // Weapons Generator
            foreach (var inputWeapons in inputWeaponsJSON)
            {
                if (inputWeapons.Enchantments == null)
                {
                    Console.WriteLine("Error: Weapon enchantment list is empty");
                    continue;
                }


                // Parse Enchantments
                List<EnchantmentInfo> enchantmentInfos = new();
                foreach (var enchantmentGetter in inputWeapons.Enchantments)
                {
                    enchantmentInfos.Add(ParseEnchantments(state, weapon_enchantments_cache, enchantmentGetter));
                }

                // Generate Enchanted Weapons
                if (inputWeapons.WeaponEditorIDs != null) foreach(var weaponEditorID in inputWeapons.WeaponEditorIDs)
                {
                        Console.WriteLine("Processing Record: " + weaponEditorID);

                        // Get base weapon
                        IWeaponGetter weaponGetter;
                        if (weapons_cache.ContainsKey(weaponEditorID))
                        {
                            weaponGetter = weapons_cache[weaponEditorID];
                        } else
                        {
                            weaponGetter = state.LinkCache.Resolve<IWeaponGetter>(weaponEditorID);
                        }
                    

                    LeveledItem LItemEnchWeapon = CreateLItemEnch(state, inputWeapons.LeveledListPrefix + weaponEditorID + inputWeapons.LeveledListSuffix);

                    foreach (var enchantmentInfo in enchantmentInfos)
                    {
                        string enchanted_weapon_EditorID = "Ench_" + weaponEditorID + "_" + enchantmentInfo.EditorID?.Replace("Ench", "");

                        if (state.LinkCache.TryResolve<IWeaponGetter>(enchanted_weapon_EditorID, out var Origenchanted_weapon)) // Get enchanted weapon if it already exists
                        {
                            if (LItemEnchWeapon.Entries != null)
                            {
                                bool add_to_leveled_list = true;
                                foreach (var entry in LItemEnchWeapon.Entries)
                                {
                                    if (entry.Data?.Reference == Origenchanted_weapon)
                                    {
                                        add_to_leveled_list = false;
                                        break;
                                    }
                                }
                                if (add_to_leveled_list) LItemEnchWeapon.Entries.Add(CreateLeveledItemEntry(1, Origenchanted_weapon.FormKey));
                            }
                        } else
                        {
                                Weapon enchanted_weapon = new(state.PatchMod, enchanted_weapon_EditorID)
                                {
                                    Name = enchantmentInfo.Prefix + weaponGetter.Name + enchantmentInfo.Suffix,
                                    EnchantmentAmount = enchantmentInfo.EnchantmentAmount
                            };
                            enchanted_weapon.ObjectEffect.SetTo(enchantmentInfo.Enchantment); // Set enchantment to weapon
                            enchanted_weapon.Template.SetTo(weaponGetter); // Set template to base weapon
                            if (weaponGetter.BasicStats != null)
                            {
                                enchanted_weapon.BasicStats = new();
                                enchanted_weapon.BasicStats.Value = weaponGetter.BasicStats.Value;
                            }
                            if (weaponGetter.VirtualMachineAdapter != null)
                                {
                                    enchanted_weapon.VirtualMachineAdapter = weaponGetter.VirtualMachineAdapter.DeepCopy();
                                }

                            state.PatchMod.Weapons.Set(enchanted_weapon);

                            LItemEnchWeapon.Entries?.Add(CreateLeveledItemEntry(1, enchanted_weapon.FormKey)); // Add to Leveled List
                        }
                    }

                    state.PatchMod.LeveledItems.Set(LItemEnchWeapon);
                }

            }
            weapons_cache.Clear();
            weapon_enchantments_cache.Clear();


            // Armor Generator
            foreach (var inputArmors in inputArmorsJSONs)
            {
                if (inputArmors.Enchantments == null)
                {
                    Console.WriteLine("Error: Armor enchantment list is empty");
                    continue;
                }


                // Parse Enchantments
                List<EnchantmentInfo> enchantmentInfos = new();
                foreach (var enchantmentGetter in inputArmors.Enchantments)
                {
                    enchantmentInfos.Add(ParseEnchantments(state, armor_enchantments_cache, enchantmentGetter));
                }

                // Generate Enchanted Armor
                if (inputArmors.ArmorEditorIDs != null) foreach (var armorEditorID in inputArmors.ArmorEditorIDs)
                    {
                        Console.WriteLine("Processing Record: " + armorEditorID);

                        // Get base armor
                        IArmorGetter armorGetter;
                        if (armor_cache.ContainsKey(armorEditorID))
                        {
                            armorGetter = armor_cache[armorEditorID];
                        }
                        else
                        {
                            armorGetter = state.LinkCache.Resolve<IArmorGetter>(armorEditorID);
                        }


                        LeveledItem LItemEnchArmor = CreateLItemEnch(state, inputArmors.LeveledListPrefix + armorEditorID + inputArmors.LeveledListSuffix);

                        foreach (var enchantmentInfo in enchantmentInfos)
                        {
                            string enchanted_armor_EditorID = "Ench_" + armorEditorID + "_" + enchantmentInfo.EditorID?.Replace("Ench", "");

                            if (state.LinkCache.TryResolve<IArmorGetter>(enchanted_armor_EditorID, out var Origenchanted_armor)) // Get enchanted weapon if it already exists
                            {
                                if (LItemEnchArmor.Entries != null)
                                {
                                    bool add_to_leveled_list = true;
                                    foreach (var entry in LItemEnchArmor.Entries)
                                    {
                                        if (entry.Data?.Reference == Origenchanted_armor)
                                        {
                                            add_to_leveled_list = false;
                                            break;
                                        }
                                    }
                                    if (add_to_leveled_list) LItemEnchArmor.Entries.Add(CreateLeveledItemEntry(1, Origenchanted_armor.FormKey));
                                }
                            }
                            else
                            {
                                Armor enchanted_armor = new(state.PatchMod, enchanted_armor_EditorID)
                                {
                                    Name = enchantmentInfo.Prefix + armorGetter.Name + enchantmentInfo.Suffix,
                                    Value = armorGetter.Value
                                };
                                enchanted_armor.ObjectEffect.SetTo(enchantmentInfo.Enchantment); // Set enchantment to weapon
                                enchanted_armor.TemplateArmor.SetTo(armorGetter); // Set template to base weapon
                                if (armorGetter.VirtualMachineAdapter != null)
                                {
                                    enchanted_armor.VirtualMachineAdapter = armorGetter.VirtualMachineAdapter.DeepCopy();
                                }

                                state.PatchMod.Armors.Set(enchanted_armor);

                                LItemEnchArmor.Entries?.Add(CreateLeveledItemEntry(1, enchanted_armor.FormKey)); // Add to Leveled List
                            }
                        }

                        state.PatchMod.LeveledItems.Set(LItemEnchArmor);
                    }

            }

        } // End of Patching
    }
}
