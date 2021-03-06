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
    public class LeveledListInfo
    {
        public string LeveledListPrefix { get; set; } = "SublistEnchWeapon_";
        public string LeveledListSuffix { get; set; } = "";
        public string Mode { get; set; } = "Add";
    }

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
        public List<LeveledListInfo>? LeveledLists { get; set; }
    }

    public class InputThing
    {
        public SortedDictionary<int, List<EnchantmentJSON>> Enchantments { get; } = new();
        public SortedDictionary<int, List<ItemJSON>> Weapons { get; } = new();
        public SortedDictionary<int, List<ItemJSON>> Armors { get; } = new();
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

        public static List<InputThing> GetInputs(String path, List<String> enabledMods)
        {
            List<InputThing> inputs = new();
            foreach (var folder in Directory.EnumerateDirectories(path))
            {
                Console.WriteLine("Reading folder: " + folder);
                InputThing input = new();

                foreach (var file in Directory.GetFiles(folder))
                {
                    string rawJSON = ReadInputFile(file);
                    var inputJSON = JsonConvert.DeserializeObject<InputJSON>(rawJSON);
                    if (inputJSON == null) throw new Exception("Cannot read file \"" + file + "\"!");

                    if (inputJSON.RequiredMods != null && !CheckRequiredMods(enabledMods, inputJSON.RequiredMods)) continue;

                    if (inputJSON.Enchantments != null)
                    {
                        if (!input.Enchantments.ContainsKey(inputJSON.PriorityOrder)) input.Enchantments[inputJSON.PriorityOrder] = new List<EnchantmentJSON>();
                        foreach (var item in inputJSON.Enchantments)
                        {
                            input.Enchantments[inputJSON.PriorityOrder].Add(item);
                            Console.WriteLine("adding enchantment: " + item.EditorID);
                        }
                    }
                    if (inputJSON.Weapons != null)
                    {
                        if (!input.Weapons.ContainsKey(inputJSON.PriorityOrder)) input.Weapons[inputJSON.PriorityOrder] = new List<ItemJSON>();
                        foreach (var item in inputJSON.Weapons)
                        {
                            input.Weapons[inputJSON.PriorityOrder].Add(item);
                            Console.WriteLine("adding weapon: " + item.EditorID);
                        }
                    }
                    if (inputJSON.Armors != null)
                    {
                        if (!input.Armors.ContainsKey(inputJSON.PriorityOrder)) input.Armors[inputJSON.PriorityOrder] = new List<ItemJSON>();
                        foreach (var item in inputJSON.Armors)
                        {
                            input.Armors[inputJSON.PriorityOrder].Add(item);
                            Console.WriteLine("adding armor: " + item.EditorID);
                        }
                    }
                }
                inputs.Add(input);
            }

            return inputs;
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

        public static void AddToLeveledList(LeveledItem leveledlist, FormKey item)
        {
            if (leveledlist.Entries == null)
            {
                leveledlist.Entries = new();
            }
            leveledlist.Entries.Add(CreateLeveledItemEntry(1, item));
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

        public static List<EnchantmentInfo> ParseEnchantments(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, List<EnchantmentJSON> enchantmentJSONs)
        {
            List<EnchantmentInfo> enchantmentInfos = new();
            foreach (var enchantmentJSON in enchantmentJSONs)
            {
                if (string.IsNullOrEmpty(enchantmentJSON.EditorID) || string.IsNullOrEmpty(enchantmentJSON.FormKey)) throw new Exception("ERROR: enchantment does not have a formkey or editorID specified");

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
                    LeveledLists = new List<LeveledListInfo>()
                };

                if (enchantmentJSON.LeveledLists != null)
                {
                    foreach (var leveledlistJSON in enchantmentJSON.LeveledLists)
                    {
                        enchantmentGetter.LeveledLists.Add(new LeveledListInfo()
                        {
                            LeveledListPrefix = leveledlistJSON.LeveledListPrefix,
                            LeveledListSuffix = leveledlistJSON.LeveledListSuffix,
                            Mode = leveledlistJSON.Mode
                        });
                    }
                }

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
                // Console.WriteLine("Leveled List \"" + editorID + "\" already exists in plugin \"" + leveledlist_Original.FormKey.ModKey.ToString() + "\", copying as override and appending changes");
                leveledlist = leveledlist_Original.DeepCopy();
            }
            else
            { // Create Leveled List
                alreadyExists = false;
                leveledlist = new LeveledItem(state.PatchMod);
                leveledlist.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
                leveledlist.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                leveledlist.EditorID = editorID;
                leveledlist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
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

        public static Armor GetEnchantedArmor(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, string editorID, bool checkExistingGenerated, out bool alreadyExists)
        {
            Armor armor;

            if (checkExistingGenerated && state.LinkCache.TryResolve<IArmorGetter>(editorID, out var armor_Original))
            { // Get Enchanted Weapon if it already exists
                alreadyExists = true;
                armor = armor_Original.DeepCopy();
            }
            else
            { // Create Enchanted Weapo
                alreadyExists = false;
                armor = new Armor(state.PatchMod)
                {
                    EditorID = editorID
                };
                state.PatchMod.Armors.Set(armor);
            }
            return armor;
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
            List<String> enabledMods = new();
            foreach (var mod in state.LoadOrder)
            {
                if (mod.Value.Enabled)
                {
                    Console.WriteLine("enabledmod: " + mod.Value.ModKey.ToString());
                    enabledMods.Add(mod.Value.ModKey.ToString());
                }
            }

            // Read JSON Files
            Config config = GetConfig(Path.Combine(state.ExtraSettingsDataPath, "config.hjson"));
            List<InputThing> inputs = GetInputs(state.ExtraSettingsDataPath + backslash + "input", enabledMods);

            //Generate Items
            foreach (var input in inputs)
            {
                foreach (var enchantmentList in input.Enchantments.Values)
                {
                    // Parse Enchantments
                    List<EnchantmentInfo> enchantmentInfos = ParseEnchantments(state, enchantmentList);

                    foreach (var enchantmentInfo in enchantmentInfos)
                    {
                        Console.WriteLine("Processing Enchantment: " + enchantmentInfo.EditorID);

                        // Generate Enchanted Weapons
                        if (input.Weapons != null)
                        {
                            foreach (var itemList in input.Weapons.Values)
                            {
                                foreach (var itemInput in itemList)
                                {
                                    if (itemInput.RequiredMods != null && CheckRequiredMods(enabledMods, itemInput.RequiredMods)) continue;

                                    var formKey = itemInput.FormKey;
                                    var editorID = itemInput.EditorID;
                                    if (editorID == null || formKey == null)
                                    {
                                        throw new Exception("weapon formkey or editorid is null");
                                    }

                                    var enchanted_item_EditorID = "Ench_" + editorID + "_" + enchantmentInfo.EditorID;

                                    var itemGetter = state.LinkCache.Resolve<IWeaponGetter>(FormKey.Factory(formKey)); // Get template item
                                    var enchanted_item = GetEnchantedWeapon(state, enchanted_item_EditorID, config.CheckExistingGenerated, out var EnchantedItemAlreadyExists);
                                    var enchanted_item_name = enchantmentInfo.Prefix + itemGetter.Name + enchantmentInfo.Suffix;

                                    if (!EnchantedItemAlreadyExists)
                                    { // Create Enchanted Item

                                        enchanted_item.Name = enchanted_item_name;
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

                                        Console.WriteLine("Generating weapon \"" + enchanted_item_EditorID + "\"");
                                        state.PatchMod.Weapons.Set(enchanted_item);
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
                                        }
                                        else
                                        {
                                            if (itemInput.Value != null && enchanted_item.BasicStats.Value != itemInput.Value)
                                            {
                                                enchanted_item.BasicStats.Value = (uint)itemInput.Value;
                                                copyAsOverride = true;
                                            }
                                            else if (itemGetter.BasicStats != null && enchanted_item.BasicStats.Value != itemGetter.BasicStats.Value)
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

                                        // Set Name
                                        if (enchanted_item.Name != enchanted_item_name)
                                        {
                                            enchanted_item.Name = enchanted_item_name;
                                            copyAsOverride = true;
                                        }

                                        //Set Scripts
                                        if (enchanted_item.VirtualMachineAdapter != itemGetter.VirtualMachineAdapter)
                                        {
                                            if (itemGetter.VirtualMachineAdapter == null)
                                            {
                                                enchanted_item.VirtualMachineAdapter = null;
                                            }
                                            else
                                            {
                                                enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();
                                            }
                                            copyAsOverride = true;
                                        }

                                        if (copyAsOverride)
                                        {
                                            Console.WriteLine("Altering " + enchanted_item_EditorID);
                                            state.PatchMod.Weapons.Set(enchanted_item);
                                        }
                                    }

                                    // Set Leveled Lists
                                    if (enchantmentInfo.LeveledLists != null)
                                    {
                                        foreach(var leveledlistinfo in enchantmentInfo.LeveledLists)
                                        {
                                            var leveledlist = GetLeveledList(state, leveledlistinfo.LeveledListPrefix + editorID + leveledlistinfo.LeveledListSuffix, config.CheckExistingGenerated, out var leveledListAlreadyExists); // Get leveled list
                                            
                                            if (leveledListAlreadyExists)
                                            {
                                                if (leveledlist.Entries != null)
                                                {
                                                    bool addtoleveledlist = true;
                                                    foreach (var entry in leveledlist.Entries)
                                                    {
                                                        if (entry.Data?.Reference.FormKey == enchanted_item.FormKey)
                                                        {
                                                            if (leveledlistinfo.Mode == "Remove")
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
                            }
                            
                        }

                        // Generate Enchanted Armors
                        if (input.Armors != null)
                        {
                            foreach (var itemList in input.Armors.Values)
                            {
                                foreach (var itemInput in itemList)
                                {
                                    if (itemInput.RequiredMods != null && CheckRequiredMods(enabledMods, itemInput.RequiredMods)) continue;

                                    var formKey = itemInput.FormKey;
                                    var editorID = itemInput.EditorID;
                                    if (editorID == null || formKey == null)
                                    {
                                        throw new Exception("armor formkey or editorid is null");
                                    }

                                    var enchanted_item_EditorID = "Ench_" + editorID + "_" + enchantmentInfo.EditorID;

                                    var itemGetter = state.LinkCache.Resolve<IArmorGetter>(FormKey.Factory(formKey)); // Get template item
                                    var enchanted_item = GetEnchantedArmor(state, enchanted_item_EditorID, config.CheckExistingGenerated, out var EnchantedItemAlreadyExists);
                                    var enchanted_item_name = enchantmentInfo.Prefix + itemGetter.Name + enchantmentInfo.Suffix;

                                    if (!EnchantedItemAlreadyExists)
                                    { // Create Enchanted Item

                                        enchanted_item.Name = enchanted_item_name;
                                        enchanted_item.EnchantmentAmount = enchantmentInfo.EnchantmentAmount;
                                        enchanted_item.ObjectEffect.SetTo(enchantmentInfo.Enchantment); // Set enchantment to item
                                        enchanted_item.TemplateArmor.SetTo(itemGetter); // Set template to base item
                                        if (itemGetter.VirtualMachineAdapter != null) enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();

                                        // Set Value
                                        if (itemInput.Value != null)
                                        {
                                            enchanted_item.Value = (uint)itemInput.Value;
                                        }
                                        else
                                        {
                                            enchanted_item.Value = itemGetter.Value;
                                        }

                                        Console.WriteLine("Generating armor \"" + enchanted_item_EditorID + "\"");
                                        state.PatchMod.Armors.Set(enchanted_item);
                                    }
                                    else
                                    {
                                        bool copyAsOverride = false;

                                        // Set Value
                                        if (itemInput.Value != null)
                                        {
                                            if (enchanted_item.Value != itemInput.Value)
                                            {
                                                enchanted_item.Value = (uint)itemInput.Value;
                                                copyAsOverride = true;
                                            }
                                        }
                                        else
                                        {
                                            if (enchanted_item.Value != itemGetter.Value)
                                            {
                                                enchanted_item.Value = itemGetter.Value;
                                                copyAsOverride = true;
                                            }
                                        }

                                        // Set Name
                                        if (enchanted_item.Name != enchanted_item_name)
                                        {
                                            enchanted_item.Name = enchanted_item_name;
                                            copyAsOverride = true;
                                        }

                                        //Set Scripts
                                        if (enchanted_item.VirtualMachineAdapter != itemGetter.VirtualMachineAdapter)
                                        {
                                            if (itemGetter.VirtualMachineAdapter == null)
                                            {
                                                enchanted_item.VirtualMachineAdapter = null;
                                            } else
                                            {
                                                enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();
                                            }
                                            copyAsOverride = true;
                                        }

                                        if (copyAsOverride)
                                        {
                                            Console.WriteLine("Altering " + enchanted_item_EditorID);
                                            state.PatchMod.Armors.Set(enchanted_item);
                                        }
                                    }

                                    // Set Leveled Lists
                                    if (enchantmentInfo.LeveledLists != null)
                                    {
                                        foreach (var leveledlistinfo in enchantmentInfo.LeveledLists)
                                        {
                                            var leveledlist = GetLeveledList(state, leveledlistinfo.LeveledListPrefix + editorID + leveledlistinfo.LeveledListSuffix, config.CheckExistingGenerated, out var leveledListAlreadyExists); // Get leveled list

                                            if (leveledListAlreadyExists)
                                            {
                                                if (leveledlist.Entries != null)
                                                {
                                                    bool addtoleveledlist = true;
                                                    foreach (var entry in leveledlist.Entries)
                                                    {
                                                        if (entry.Data?.Reference.FormKey == enchanted_item.FormKey)
                                                        {
                                                            if (leveledlistinfo.Mode == "Remove")
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
                            }
                        }
                    }
                }  
            }
        } // End of Patching
    }
}
