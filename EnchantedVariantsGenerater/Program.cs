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
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public IObjectEffectGetter Enchantment { get; set; }
        public string EditorID { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string? Sublist { get; set; }
        public ushort? EnchantmentAmount { get; set; }
        public string Mode { get; set; } = "Add";
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

        public static String ReadInputFile(String filePath)
        {
            string rawJSON;
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

        public static List<InputJSON> GetInput(String path)
        {
            List<InputJSON> inputJSONList = new();
            foreach (var filePath in Directory.GetFiles(path))
            {
                if (filePath == null) throw new Exception();
                string rawJSON = ReadInputFile(filePath);
                var inputJSON = JsonConvert.DeserializeObject<InputJSON>(rawJSON);
                if (inputJSON == null) throw new Exception("Cannot read file \"" + filePath + "\"!");
                inputJSONList.Add(inputJSON);
            }
            return inputJSONList;
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

        /*
        public static bool CheckIfInLeveledList(LeveledItem leveledlist, FormKey item)
        {
            if (leveledlist.Entries != null)
            {
                foreach (var entry in leveledlist.Entries)
                {
                    if (entry.Data?.Reference.FormKey == item)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        */

        public static void AddToLeveledList(LeveledItem leveledlist, FormKey item)
        {
            if (leveledlist.Entries == null)
            {
                leveledlist.Entries = new();
            }
            leveledlist.Entries.Add(CreateLeveledItemEntry(1, item));
        }

        public static void LeveledListThing(LeveledItem leveledlist, FormKey item, String mode)
        {
            if (leveledlist.Entries != null)
            {
                foreach (var entry in leveledlist.Entries)
                {
                    if (entry.Data?.Reference.FormKey == item)
                    {
                        if (mode == "Remove")
                        {
                            leveledlist.Entries.Remove(entry);
                        }
                        return;
                    }
                }
                if (mode == "Add")
                {
                    AddToLeveledList(leveledlist, item);
                }
            } else
            {
                if (mode == "Add")
                {
                    AddToLeveledList(leveledlist, item);
                }
            }
        }

        public static bool CheckRequiredMods(List<string> enabledMods, string[] requiredMods)
        {
            foreach (var requiredMod in requiredMods)
            {
                if (enabledMods.Contains(requiredMod))
                {
                    return true;
                }
            }
            return false;
        }

        public static List<EnchantmentInfo> ParseEnchantments(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, EnchantmentJSON[] enchantmentJSONs, List<string> enabledMods)
        {
            List<EnchantmentInfo> enchantmentInfos = new();
            foreach (var enchantmentJSON in enchantmentJSONs)
            {
                if (string.IsNullOrEmpty(enchantmentJSON.EditorID) || string.IsNullOrEmpty(enchantmentJSON.FormKey)) throw new Exception("ERROR: enchantment does not have a formkey or editorID specified");
                if (enchantmentJSON.RequiredMods != null && CheckRequiredMods(enabledMods, enchantmentJSON.RequiredMods)) continue;

                IObjectEffectGetter enchantment;
                if (state.LinkCache.TryResolve<IObjectEffectGetter>(FormKey.Factory(enchantmentJSON.FormKey), out var enchant))
                {
                    enchantment = enchant;
                }
                else
                {
                    throw new Exception("Cannot find enchantment with FormKey \"" + enchantmentJSON.FormKey.ToString() + "\"");
                }

                var enchantmentGetter = new EnchantmentInfo
                {
                    EnchantmentAmount = (ushort?)enchantmentJSON.EnchantmentAmount,
                    Enchantment = enchantment,
                    Prefix = enchantmentJSON.Prefix,
                    Suffix = enchantmentJSON.Suffix,
                    EditorID = enchantmentJSON.EditorID,
                };
                enchantmentInfos.Add(enchantmentGetter);
            }
            return enchantmentInfos;
        }

        public static LeveledItem GetLeveledList(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, string editorID, bool checkExistingGenerated, out bool alreadyExists)
        {
            LeveledItem leveledlist;

            if (checkExistingGenerated && state.LinkCache.TryResolve<ILeveledItemGetter>(editorID, out var leveledlist_Original))
            { // Get Leveled List if it already exists
                alreadyExists = true;
                Console.WriteLine("Leveled List \"" + editorID + "\" already exists in plugin \"" + leveledlist_Original.FormKey.ModKey.ToString() + "\", copying as override and appending changes");
                leveledlist = leveledlist_Original.DeepCopy();
                state.PatchMod.LeveledItems.Set(leveledlist);
            }
            else
            { // Create Leveled List
                alreadyExists = false;
                leveledlist = new LeveledItem(state.PatchMod);
                leveledlist.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
                leveledlist.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                leveledlist.EditorID = editorID;
                leveledlist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                state.PatchMod.LeveledItems.Set(leveledlist);
            }
            return leveledlist;
        }

        public static Weapon GetEnchantedWeapon(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, string editorID, bool checkExistingGenerated, out bool alreadyExists)
        {
            Weapon weapon;

            if (checkExistingGenerated && state.LinkCache.TryResolve<IWeaponGetter>(editorID, out var weapon_Original))
            { // Get Enchanted Weapon if it already exists
                alreadyExists = true;
                weapon = weapon_Original.DeepCopy();
            }
            else
            { // Create Enchanted Weapo
                alreadyExists = false;
                weapon = new Weapon(state.PatchMod)
                {
                    EditorID = editorID
                };
                state.PatchMod.Weapons.Set(weapon);
            }
            return weapon;
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
            List<InputJSON> inputs = GetInput(state.ExtraSettingsDataPath + backslash + "input");

            List<String> enabledMods = new();
            foreach (var mod in state.LoadOrder) {
                if (mod.Value.Enabled)
                {
                    enabledMods.Add(mod.Key.ToString());
                }
            }

            //Generate Items
            foreach (var input in inputs)
            {
                if (input.RequiredMods != null && CheckRequiredMods(enabledMods, input.RequiredMods)) continue;

                if (input.Enchantments == null)
                {
                    Console.WriteLine("Error: Enchantment list is empty");
                    continue;
                }

                // Parse Enchantments
                List<EnchantmentInfo> enchantmentInfos = ParseEnchantments(state, input.Enchantments, enabledMods);

                foreach (var enchantmentInfo in enchantmentInfos)
                {
                    // Generate Enchanted Weapons
                    if (input.Weapons != null)
                    {
                        foreach (var itemInput in input.Weapons)
                        {
                            if (itemInput.RequiredMods != null && CheckRequiredMods(enabledMods, itemInput.RequiredMods)) continue;

                            var formKey = itemInput.FormKey;
                            var editorID = itemInput.EditorID;
                            if (editorID == null || formKey == null)
                            {
                                throw new Exception("weapon formkey or editorid is null");
                            }

                            Console.WriteLine("Processing Record: " + editorID);

                            var enchanted_item_EditorID = "Ench_" + editorID + "_" + enchantmentInfo.EditorID;

                            var itemGetter = state.LinkCache.Resolve<IWeaponGetter>(FormKey.Factory(formKey)); // Get template item
                            var leveledlist = GetLeveledList(state, input.LeveledListPrefix + editorID + input.LeveledListSuffix, config.CheckExistingGenerated, out var leveledListAlreadyExists); // Get leveled list
                            var enchanted_item = GetEnchantedWeapon(state, enchanted_item_EditorID, config.CheckExistingGenerated, out var EnchantedItemAlreadyExists);

                            if (!EnchantedItemAlreadyExists)
                            { // Create Enchanted Item

                                enchanted_item.Name = enchantmentInfo.Prefix + itemGetter.Name + enchantmentInfo.Suffix;
                                enchanted_item.EnchantmentAmount = enchantmentInfo.EnchantmentAmount;
                                enchanted_item.ObjectEffect.SetTo(enchantmentInfo.Enchantment); // Set enchantment to item
                                enchanted_item.Template.SetTo(itemGetter); // Set template to base item
                                if (itemGetter.VirtualMachineAdapter != null) enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();

                                // Set Value
                                enchanted_item.BasicStats = new();
                                if (itemInput.Value != null)
                                {
                                    enchanted_item.BasicStats.Value = (uint)itemInput.Value;
                                }
                                else
                                {
                                    if (itemGetter.BasicStats != null)
                                    {
                                        enchanted_item.BasicStats.Value = itemGetter.BasicStats.Value;
                                    }
                                }

                                Console.WriteLine("Generating item \"" + enchanted_item_EditorID + "\"");
                                state.PatchMod.Weapons.Set(enchanted_item);


                                // Add to Leveled List

                            }
                            else
                            {
                                bool copyAsOverride = false;

                                // Set Value
                                if (enchanted_item.BasicStats == null)
                                {
                                    if (itemInput.Value != null)
                                    {
                                        enchanted_item.BasicStats = new();
                                        enchanted_item.BasicStats.Value = (uint)itemInput.Value;
                                        copyAsOverride = true;
                                    }
                                } else
                                {
                                    if (itemInput.Value != null && enchanted_item.BasicStats.Value != itemInput.Value)
                                    {
                                        enchanted_item.BasicStats.Value = (uint)itemInput.Value;
                                        copyAsOverride = true;
                                    } else if (itemGetter.BasicStats != null && enchanted_item.BasicStats.Value != itemGetter.BasicStats.Value)
                                    {
                                        enchanted_item.BasicStats.Value = itemGetter.BasicStats.Value;
                                        copyAsOverride = true;
                                    }
                                }

                                // Set Enchantment Amount
                                if (enchanted_item.EnchantmentAmount != enchantmentInfo.EnchantmentAmount)
                                {
                                    enchanted_item.EnchantmentAmount = enchantmentInfo.EnchantmentAmount;
                                    copyAsOverride = true;
                                }

                                if (copyAsOverride) state.PatchMod.Weapons.Set(enchanted_item);

                                
                            }

                            // Set Leveled List
                            if (leveledListAlreadyExists)
                            {
                                if (leveledlist.Entries != null)
                                {
                                    bool addtoleveledlist = true;
                                    foreach (var entry in leveledlist.Entries)
                                    {
                                        if (entry.Data?.Reference.FormKey == enchanted_item.FormKey)
                                        {
                                            if (enchantmentInfo.Mode == "Remove")
                                            {
                                                leveledlist.Entries.Remove(entry);
                                                state.PatchMod.LeveledItems.Set(leveledlist);
                                            }
                                            else
                                            {
                                                addtoleveledlist = false;
                                            }
                                            break;
                                        }
                                    }
                                    if (addtoleveledlist)
                                    {
                                        AddToLeveledList(leveledlist, enchanted_item.FormKey);
                                        state.PatchMod.LeveledItems.Set(leveledlist);
                                    }
                                }
                            }
                            else
                            {
                                AddToLeveledList(leveledlist, enchanted_item.FormKey);
                                state.PatchMod.LeveledItems.Set(leveledlist);
                            }
                        }
                    }
                }

                /*
                
                // Parse Armors
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
                */
            }


        } // End of Patching
    }
}
