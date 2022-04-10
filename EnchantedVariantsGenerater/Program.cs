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
using Hjson;
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
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public class Program
    {
        private static readonly String backslash = "\\";

        public static Config GetConfig(String filePath)
        {
            Config? config = JsonConvert.DeserializeObject<Config>(HjsonValue.Load(filePath).ToString());
            if (config == null) throw new Exception("config does not exist");

            if (config.CheckExistingGenerated) Console.WriteLine("CheckExistingGenerated = true");

            return config;
        }

        public static String readInputFile(String filePath)
        {
            string rawJSON = "";
            if (filePath.EndsWith(".json"))
            {
                Console.WriteLine("Reading JSON file \"" + filePath + "\"");
                rawJSON = File.ReadAllText(filePath);
            }
            else if (filePath.EndsWith(".hjson"))
            {
                Console.WriteLine("Reading HJSON file \"" + filePath + "\"");
                rawJSON = HjsonValue.Load(filePath).ToString();
            }
            else
            {
                throw new Exception("Unknown file in Input directory: \"" + filePath + "\"");
            }
            return rawJSON;
        }

        public static List<InputWeaponsJSON> GetWeaponsInput(String path)
        {
            List<InputWeaponsJSON> inputWeaponsJSONList = new();
            foreach (var filePath in Directory.GetFiles(path))
            {
                if (filePath == null) throw new Exception();
                string rawJSON = readInputFile(filePath);
                var inputWeaponsJSON = JsonConvert.DeserializeObject<InputWeaponsJSON>(rawJSON);
                if (inputWeaponsJSON == null) throw new Exception("Cannot read file \"" + filePath + "\"!");
                inputWeaponsJSONList.Add(inputWeaponsJSON);
            }
            return inputWeaponsJSONList;
        }

        public static List<InputArmorsJSON> GetArmorsInput(String path)
        {
            List<InputArmorsJSON> inputWeaponsJSONList = new();
            foreach (var filePath in Directory.GetFiles(path))
            {
                if (filePath == null) throw new Exception();
                string rawJSON = readInputFile(filePath);
                var inputArmorsJSON = JsonConvert.DeserializeObject<InputArmorsJSON>(rawJSON);
                if (inputArmorsJSON == null) throw new Exception("Cannot read file \"" + filePath + "\"!");
                inputWeaponsJSONList.Add(inputArmorsJSON);
            }
            return inputWeaponsJSONList;
        }

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

        public static List<EnchantmentInfo> ParseEnchantments(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, EnchantmentJSON[] enchantmentJSONs)
        {
            List<EnchantmentInfo> enchantmentInfos = new();
            foreach (var enchantmentJSON in enchantmentJSONs)
            {
                IObjectEffectGetter enchantment;
                if (enchantmentJSON.FormKey != null)
                {
                    if (state.LinkCache.TryResolve<IObjectEffectGetter>(FormKey.Factory(enchantmentJSON.FormKey), out var enchant))
                    {
                        enchantment = enchant;
                    } 
                    else
                    {
                        throw new Exception("Cannot find enchantment with FormKey \"" + enchantmentJSON.FormKey.ToString() + "\"");
                    }
                }
                else if (enchantmentJSON.EditorID != null)
                {
                    enchantment = state.LinkCache.Resolve<IObjectEffectGetter>(enchantmentJSON.EditorID);
                }
                else
                {
                    throw new Exception("ERROR: enchantment does not have a formkey or editorID specified");
                }
                var enchantmentGetter = new EnchantmentInfo
                {
                    EnchantmentAmount = (ushort?)enchantmentJSON.EnchantmentAmount,
                    Enchantment = enchantment,
                    Prefix = enchantmentJSON.Prefix,
                    Suffix = enchantmentJSON.Suffix,
                    Level = enchantmentJSON.Level,
                    EditorID = enchantment.EditorID,
                };
                enchantmentInfos.Add(enchantmentGetter);
            }
            return enchantmentInfos;
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

        // Run Patch
        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            // Read JSON Files
            Config config = GetConfig(Path.Combine(state.ExtraSettingsDataPath, "config.hjson"));
            List<InputWeaponsJSON> inputWeaponsJSON = GetWeaponsInput(state.ExtraSettingsDataPath + backslash + "input" + backslash + "weapons");
            List<InputArmorsJSON> inputArmorsJSONs = GetArmorsInput(state.ExtraSettingsDataPath + backslash + "input" + backslash + "armors");

            // Weapons Generator
            foreach (var input in inputWeaponsJSON)
            {
                if (input.Enchantments == null)
                {
                    Console.WriteLine("Error: Weapon enchantment list is empty");
                    continue;
                }

                // Parse Weapons
                List<IWeaponGetter> weapons = new();
                if (input.WeaponFormKeys != null)
                {
                    foreach (var weapon in input.WeaponFormKeys)
                    {
                        weapons.Add(state.LinkCache.Resolve<IWeaponGetter>(FormKey.Factory(weapon)));
                    }
                }
                if (input.WeaponEditorIDs != null)
                {
                    foreach (var weapon in input.WeaponEditorIDs)
                    {
                        weapons.Add(state.LinkCache.Resolve<IWeaponGetter>(weapon));
                    }
                }

                // Parse Enchantments
                List<EnchantmentInfo> enchantmentInfos = ParseEnchantments(state, input.Enchantments);

                // Generate Enchanted Weapons
                foreach (var itemGetter in weapons)
                {
                    Console.WriteLine("Processing Record: " + itemGetter.EditorID);

                    // Create Leveled List
                    string LItemEnchWeapon_EditorID = input.LeveledListPrefix + itemGetter.EditorID + input.LeveledListSuffix;
                    bool old_leveled_list_exists = false;
                    LeveledItem LItemEnchItem;
                    if (config.CheckExistingGenerated && state.LinkCache.TryResolve<ILeveledItemGetter>(LItemEnchWeapon_EditorID, out var LItemEnchWeaponOriginal)) // Get Leveled List if it already exists
                    {
                        LItemEnchItem = (LeveledItem)LItemEnchWeaponOriginal;
                        old_leveled_list_exists = true;
                    }
                    else
                    {
                        LItemEnchItem = CreateLItemEnch(state, input.LeveledListPrefix + itemGetter.EditorID + input.LeveledListSuffix);
                    }

                    foreach (var enchantmentInfo in enchantmentInfos)
                    {
                        string enchanted_item_EditorID = "Ench_" + itemGetter.EditorID + "_" + enchantmentInfo.EditorID?.Replace("Ench", "");

                        if (config.CheckExistingGenerated && state.LinkCache.TryResolve<IWeaponGetter>(enchanted_item_EditorID, out var Origenchanted_weapon)) // Get enchanted weapon if it already exists
                        {
                            // Add to leveled list
                            if (LItemEnchItem.Entries != null)
                            {
                                // Check if already exists in leveled list
                                bool add_to_leveled_list = true;
                                if (old_leveled_list_exists)
                                {
                                    foreach (var entry in LItemEnchItem.Entries)
                                    {
                                        if (entry.Data?.Reference == Origenchanted_weapon)
                                        {
                                            // Change level
                                            if (entry.Data != null && entry.Data.Level != enchantmentInfo.Level)
                                            {
                                                entry.Data.Level = enchantmentInfo.Level;
                                            }

                                            add_to_leveled_list = false;
                                            break;
                                        }
                                    }
                                }

                                if (add_to_leveled_list) LItemEnchItem.Entries.Add(CreateLeveledItemEntry(enchantmentInfo.Level, Origenchanted_weapon.FormKey));
                            }
                        } 
                        else
                        {
                            Weapon enchanted_item = new(state.PatchMod, enchanted_item_EditorID)
                            {
                                Name = enchantmentInfo.Prefix + itemGetter.Name + enchantmentInfo.Suffix,
                                EnchantmentAmount = enchantmentInfo.EnchantmentAmount
                            };
                            enchanted_item.ObjectEffect.SetTo(enchantmentInfo.Enchantment); // Set enchantment to item
                            enchanted_item.Template.SetTo(itemGetter); // Set template to base item
                            if (itemGetter.BasicStats != null)
                            {
                                enchanted_item.BasicStats = new();
                                enchanted_item.BasicStats.Value = itemGetter.BasicStats.Value;
                            }
                            if (itemGetter.VirtualMachineAdapter != null)
                            {
                                enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();
                            }

                            Console.WriteLine("Generating item \"" + enchanted_item_EditorID + "\"");

                            state.PatchMod.Weapons.Set(enchanted_item);
                            LItemEnchItem.Entries?.Add(CreateLeveledItemEntry(enchantmentInfo.Level, enchanted_item.FormKey)); // Add to Leveled List
                        }
                    }

                    state.PatchMod.LeveledItems.Set(LItemEnchItem);
                }
            }


            // Armor Generator
            foreach (var input in inputArmorsJSONs)
            {
                if (input.Enchantments == null)
                {
                    Console.WriteLine("Error: Weapon enchantment list is empty");
                    continue;
                }

                // Parse Enchantments
                List<EnchantmentInfo> enchantmentInfos = ParseEnchantments(state, input.Enchantments);

                // Parse Weapons
                List<IArmorGetter> armors = new();
                if (input.ArmorFormKeys != null)
                {
                    foreach (var weapon in input.ArmorFormKeys)
                    {
                        armors.Add(state.LinkCache.Resolve<IArmorGetter>(weapon));
                    }
                }
                if (input.ArmorFormKeys != null)
                {
                    foreach (var weapon in input.ArmorFormKeys)
                    {
                        armors.Add(state.LinkCache.Resolve<IArmorGetter>(weapon));
                    }
                }

                // Generate Enchanted Weapons
                foreach (var itemGetter in armors)
                {
                    Console.WriteLine("Processing Record: " + itemGetter.EditorID);

                    // Create Leveled List
                    string LItemEnchWeapon_EditorID = input.LeveledListPrefix + itemGetter.EditorID + input.LeveledListSuffix;
                    bool old_leveled_list_exists = false;
                    LeveledItem LItemEnchItem;
                    if (config.CheckExistingGenerated && state.LinkCache.TryResolve<ILeveledItemGetter>(LItemEnchWeapon_EditorID, out var LItemEnchWeaponOriginal)) // Get Leveled List if it already exists
                    {
                        LItemEnchItem = (LeveledItem)LItemEnchWeaponOriginal;
                        old_leveled_list_exists = true;
                    }
                    else
                    {
                        LItemEnchItem = CreateLItemEnch(state, input.LeveledListPrefix + itemGetter.EditorID + input.LeveledListSuffix);
                    }

                    foreach (var enchantmentInfo in enchantmentInfos)
                    {
                        string enchanted_item_EditorID = "Ench_" + itemGetter.EditorID + "_" + enchantmentInfo.EditorID?.Replace("Ench", "");

                        if (config.CheckExistingGenerated && state.LinkCache.TryResolve<IArmorGetter>(enchanted_item_EditorID, out var Origenchanted_weapon)) // Get enchanted weapon if it already exists
                        {
                            // Add to leveled list
                            if (LItemEnchItem.Entries != null)
                            {
                                // Check if already exists in leveled list
                                bool add_to_leveled_list = true;
                                if (old_leveled_list_exists)
                                {
                                    foreach (var entry in LItemEnchItem.Entries)
                                    {
                                        if (entry.Data?.Reference == Origenchanted_weapon)
                                        {
                                            // Change level
                                            if (entry.Data != null && entry.Data.Level != enchantmentInfo.Level)
                                            {
                                                entry.Data.Level = enchantmentInfo.Level;
                                            }

                                            add_to_leveled_list = false;
                                            break;
                                        }
                                    }
                                }

                                if (add_to_leveled_list) LItemEnchItem.Entries.Add(CreateLeveledItemEntry(enchantmentInfo.Level, Origenchanted_weapon.FormKey));
                            }
                        }
                        else
                        {
                            Armor enchanted_item = new(state.PatchMod, enchanted_item_EditorID)
                            {
                                Name = enchantmentInfo.Prefix + itemGetter.Name + enchantmentInfo.Suffix,
                                EnchantmentAmount = enchantmentInfo.EnchantmentAmount
                            };
                            enchanted_item.ObjectEffect.SetTo(enchantmentInfo.Enchantment); // Set enchantment to item
                            enchanted_item.TemplateArmor.SetTo(itemGetter); // Set template to base item
                            enchanted_item.Value = itemGetter.Value;
                            if (itemGetter.VirtualMachineAdapter != null)
                            {
                                enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();
                            }

                            state.PatchMod.Armors.Set(enchanted_item);
                            LItemEnchItem.Entries?.Add(CreateLeveledItemEntry(enchantmentInfo.Level, enchanted_item.FormKey)); // Add to Leveled List
                        }
                    }

                    state.PatchMod.LeveledItems.Set(LItemEnchItem);
                }
            }
        } // End of Patching
    }
}
