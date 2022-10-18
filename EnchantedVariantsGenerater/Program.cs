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
using System.Threading;




namespace EnchantedVariantsGenerater
{
    public class UnsafeThreadStuff
    {
        public List<Weapon> WeaponsToSet { get; set; } = new();
        public List<Armor> ArmorToSet { get; set; } = new();
        public List<LeveledItem> LeveledListsToSet { get; set; } = new();
    }

    public class LeveledListInfo
    {
        public string LeveledListPrefix { get; set; } = "SublistEnch_";
        public string LeveledListSuffix { get; set; } = "";
        public string Mode { get; set; } = "Add";
    }

    public class EnchantmentInfo
    {
        public IObjectEffectGetter Enchantment { get; set; }
        public string EditorID { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public string? Sublist { get; set; }
        public ushort? EnchantmentAmount { get; set; }
        public List<LeveledListInfo>? LeveledLists { get; set; }

        public EnchantmentInfo(string editorID, IObjectEffectGetter enchantment)
        {
            Enchantment = enchantment;
            EditorID = editorID;
        }
    }

    public class InputThing
    {
        //                EditorID, Info
        public Dictionary<String, EnchantmentInfo> Enchantments { get; } = new();
        public Dictionary<String, ItemJSON> Weapons { get; } = new();
        public Dictionary<String, ItemJSON> Armors { get; } = new();
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

        public static List<InputThing> GetInputs(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, List<String> enabledMods)
        {
            List<InputThing> inputs = new();
            foreach (var folder in Directory.EnumerateDirectories(state.ExtraSettingsDataPath + backslash + "input"))
            {
                Console.WriteLine("Reading folder: " + folder);
                InputThing input = new();

                /* TODO
                var files = new SortedDictionary<int, InputJSON>();
                foreach (var file in Directory.GetFiles(folder))
                {
                    string rawJSON = ReadInputFile(file);
                    var inputJSON = JsonConvert.DeserializeObject<InputJSON>(rawJSON);
                    if (inputJSON == null) throw new Exception("Cannot read file \"" + file + "\"!");
                    if (inputJSON.RequiredMods != null && !CheckRequiredMods(enabledMods, inputJSON.RequiredMods)) continue;
                    files.Add(inputJSON.PriorityOrder, inputJSON);
                }
                */

                var files = new List<InputJSON>();
                foreach (var file in Directory.GetFiles(folder))
                {
                    string rawJSON = ReadInputFile(file);
                    var inputJSON = JsonConvert.DeserializeObject<InputJSON>(rawJSON);
                    if (inputJSON == null) throw new Exception("Cannot read file \"" + file + "\"!");

                    if (inputJSON.RequiredMods != null && !CheckRequiredMods(enabledMods, inputJSON.RequiredMods)) continue;

                    files.Add(inputJSON);
                }

                foreach (var inputJSON in files)
                {
                    if (inputJSON.Enchantments != null)
                    {
                        foreach (var item in inputJSON.Enchantments)
                        {
                            if (string.IsNullOrEmpty(item.EditorID) || string.IsNullOrEmpty(item.FormKey)) throw new Exception("ERROR: enchantment does not have a formkey or editorID specified");
                            if (input.Enchantments.ContainsKey(item.EditorID))
                            {
                                //TODO
                            }
                            else
                            {
                                IObjectEffectGetter enchantment;
                                if (state.LinkCache.TryResolve<IObjectEffectGetter>(FormKey.Factory(item.FormKey), out var enchant))
                                {
                                    enchantment = enchant;
                                }
                                else
                                {
                                    throw new Exception("Cannot find enchantment with FormKey \"" + item.FormKey.ToString() + "\"");
                                }

                                var enchantmentGetter = new EnchantmentInfo(item.EditorID, enchantment)
                                {
                                    EnchantmentAmount = (ushort?)item.EnchantmentAmount,
                                    Prefix = item.Prefix,
                                    Suffix = item.Suffix,
                                    LeveledLists = new List<LeveledListInfo>()
                                };
                                if (item.LeveledLists != null)
                                {
                                    foreach (var leveledlistJSON in item.LeveledLists)
                                    {
                                        enchantmentGetter.LeveledLists.Add(new LeveledListInfo()
                                        {
                                            LeveledListPrefix = leveledlistJSON.LeveledListPrefix,
                                            LeveledListSuffix = leveledlistJSON.LeveledListSuffix,
                                            Mode = leveledlistJSON.Mode
                                        });
                                    }
                                }

                                // Set Overwrites
                                if (item.Overwrites != null)
                                {
                                    foreach (var overwrite in item.Overwrites)
                                    {
                                        if (overwrite.RequiredMods != null && !CheckRequiredMods(enabledMods, overwrite.RequiredMods)) continue;

                                        if (overwrite.Prefix != null)
                                        {
                                            enchantmentGetter.Prefix = overwrite.Prefix;
                                        }
                                        if (overwrite.Suffix != null)
                                        {
                                            enchantmentGetter.Suffix = overwrite.Suffix;
                                        }
                                        if (overwrite.EnchantmentAmount != null)
                                        {
                                            enchantmentGetter.EnchantmentAmount = (ushort?)overwrite.EnchantmentAmount;
                                        }
                                        if (overwrite.LeveledLists != null)
                                        {
                                            foreach (var leveledlist in overwrite.LeveledLists)
                                            {
                                                if (leveledlist.Mode == "Remove")
                                                {
                                                    foreach (var oldleveledlist in enchantmentGetter.LeveledLists)
                                                    {
                                                        if (oldleveledlist.Mode == "Add" && oldleveledlist.LeveledListPrefix == leveledlist.LeveledListPrefix && oldleveledlist.LeveledListSuffix == leveledlist.LeveledListSuffix)
                                                        {
                                                            Console.WriteLine("overwrite remove");
                                                            enchantmentGetter.LeveledLists.Remove(oldleveledlist);
                                                            break;
                                                        }
                                                    }
                                                }
                                                enchantmentGetter.LeveledLists.Add(new LeveledListInfo()
                                                {
                                                    LeveledListPrefix = leveledlist.LeveledListPrefix,
                                                    LeveledListSuffix = leveledlist.LeveledListSuffix,
                                                    Mode = leveledlist.Mode
                                                });
                                            }
                                        }
                                    }
                                }

                                input.Enchantments.Add(item.EditorID, enchantmentGetter);
                            }

                            Console.WriteLine("adding enchantment: " + item.EditorID);
                        }
                    }
                    if (inputJSON.Weapons != null)
                    {
                        foreach (var item in inputJSON.Weapons)
                        {
                            if (string.IsNullOrEmpty(item.EditorID) || string.IsNullOrEmpty(item.FormKey)) throw new Exception("ERROR: weapon does not have a formkey or editorID specified");

                            if (item.BlacklistedMods != null && CheckRequiredMods(enabledMods, item.BlacklistedMods))
                            {
                                Console.WriteLine("Blacklisted mod detected! Skipping item: " + item.EditorID);
                                continue;
                            }
                            else
                            {
                                input.Weapons.Add(item.EditorID, item);
                                Console.WriteLine("adding weapon: " + item.EditorID);
                            }
                            /*
                            if (input.Enchantments.ContainsKey(item.EditorID))
                            {
                                //TODO
                            }
                            else
                            {
                            }
                            */
                        }
                    }
                    if (inputJSON.Armors != null)
                    {
                        foreach (var item in inputJSON.Armors)
                        {
                            if (string.IsNullOrEmpty(item.EditorID) || string.IsNullOrEmpty(item.FormKey)) throw new Exception("ERROR: armor does not have a formkey or editorID specified");
                            if (item.BlacklistedMods != null && CheckRequiredMods(enabledMods, item.BlacklistedMods))
                            {
                                Console.WriteLine("Blacklisted mod detected! Skipping item: " + item.EditorID);
                                continue;
                            }
                            else
                            {
                                input.Armors.Add(item.EditorID, item);
                                Console.WriteLine("adding armor: " + item.EditorID);
                            }
                            /*
                            if (input.Enchantments.ContainsKey(item.EditorID))
                            {
                                //TODO
                            }
                            else
                            {
                            }
                            */
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
                leveledlist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
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

        public static LeveledItem GetLeveledList(ImmutableLoadOrderLinkCache linkCache, ISkyrimMod patchMod, string editorID, bool checkExistingGenerated, out bool alreadyExists)
        {
            LeveledItem leveledlist;

            if (checkExistingGenerated && linkCache.TryResolve<ILeveledItemGetter>(editorID, out var leveledlist_Original))
            { // Get Leveled List if it already exists
                alreadyExists = true;
                // Console.WriteLine("Leveled List \"" + editorID + "\" already exists in plugin \"" + leveledlist_Original.FormKey.ModKey.ToString() + "\", copying as override and appending changes");
                //leveledlist = linkCache.Resolve<ILeveledItemGetter>(leveledlist_Original.FormKey).DeepCopy();
                leveledlist = leveledlist_Original.DeepCopy();
            }
            else
            { // Create Leveled List
                alreadyExists = false;
                leveledlist = new LeveledItem(patchMod);
                leveledlist.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
                leveledlist.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                leveledlist.EditorID = editorID;
                leveledlist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
            }
            return leveledlist;
        }

        public static readonly Weapon.TranslationMask weapontranslationmark = new(defaultOn: false)
        {
            ObjectEffect = true,
            EnchantmentAmount = true,
            Name = true,
            BasicStats = true,
            VirtualMachineAdapter = true,
            Template = true,
            FormVersion = true,
        };

        public static Weapon GetEnchantedWeapon(ImmutableLoadOrderLinkCache linkCache, ISkyrimMod patchMod, string editorID, bool checkExistingGenerated, out bool alreadyExists, IFormLinkNullable<IObjectEffectGetter> enchantment, IFormLinkNullable<IWeaponGetter> template)
        {
            Weapon weapon;

            if (checkExistingGenerated && linkCache.TryResolve<IWeaponGetter>(editorID, out var weapon_Original))
            { // Get Enchanted Weapon if it already exists
                alreadyExists = true;
                weapon = weapon_Original.DeepCopy(weapontranslationmark);
            }
            else
            { // Create Enchanted Weapo
                alreadyExists = false;
                weapon = new Weapon(patchMod)
                {
                    EditorID = editorID,
                    ObjectEffect = enchantment,
                    Template = template
                };
            }
            return weapon;
        }

        public static readonly Armor.TranslationMask armortranslationmark = new(defaultOn: false)
        {
            ObjectEffect = true,
            EnchantmentAmount = true,
            VirtualMachineAdapter = true,
            Name = true,
            Value = true,
            TemplateArmor = true,
            FormVersion = true,
            WorldModel = new GenderedItem<ArmorModel.TranslationMask>(new ArmorModel.TranslationMask(defaultOn: false) { Model = new Model.TranslationMask(false) { } }, new ArmorModel.TranslationMask(defaultOn: false) { Model = new Model.TranslationMask(false) { } })
        };

        public static Armor GetEnchantedArmor(ImmutableLoadOrderLinkCache linkCache, ISkyrimMod patchMod, string editorID, bool checkExistingGenerated, out bool alreadyExists, IFormLinkNullable<IObjectEffectGetter> enchantment, IFormLinkNullable<IArmorGetter> template)
        {
            Armor armor;

            if (checkExistingGenerated && linkCache.TryResolve<IArmorGetter>(editorID, out var armor_Original))
            { // Get Enchanted Weapon if it already exists
                alreadyExists = true;
                armor = armor_Original.DeepCopy(armortranslationmark);
            }
            else
            { // Create Enchanted Weapo
                alreadyExists = false;
                armor = new Armor(patchMod)
                {
                    EditorID = editorID,
                    ObjectEffect = enchantment,
                    TemplateArmor = template
                };
            }
            return armor;
        }

        public static UnsafeThreadStuff GenerateWeapon(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Config config, InputThing input, ImmutableLoadOrderLinkCache linkCache)
        {
            UnsafeThreadStuff stuff = new();
            foreach (var item in input.Weapons.Values)
            {
                Console.WriteLine("Processing Weapon: " + item.EditorID);
                IWeaponGetter itemGetter;
                if (linkCache.TryResolve<IWeaponGetter>(FormKey.Factory(item.FormKey), out var output))
                { // Get template item
                    itemGetter = output;
                }
                else
                {
                    throw new Exception("Could not find Weapon:" + item.EditorID + " : " + item.FormKey);
                }

                Dictionary<String, LeveledItem> leveledlists = new();

                foreach (var enchantmentInfo in input.Enchantments.Values)
                {
                    var enchanted_item_EditorID = "Ench_" + item.EditorID + "_" + enchantmentInfo.EditorID;
                    var enchanted_item = GetEnchantedWeapon(linkCache, state.PatchMod, enchanted_item_EditorID, config.CheckExistingGenerated, out var EnchantedItemAlreadyExists, enchantmentInfo.Enchantment.AsNullableLink(), itemGetter.AsNullableLink());
                    var enchanted_item_name = enchantmentInfo.Prefix + itemGetter.Name + enchantmentInfo.Suffix;

                    if (!EnchantedItemAlreadyExists)
                    { // Create Enchanted Item

                        enchanted_item.Name = enchanted_item_name;
                        enchanted_item.EnchantmentAmount = enchantmentInfo.EnchantmentAmount;

                        // Set Value
                        enchanted_item.BasicStats = new();
                        if (item.Value != null)
                        {
                            enchanted_item.BasicStats.Value = (uint)item.Value;
                        }
                        else
                        {
                            if (itemGetter.BasicStats != null)
                            {
                                enchanted_item.BasicStats.Value = itemGetter.BasicStats.Value;
                            }
                        }

                        Console.WriteLine("Generating weapon \"" + enchanted_item_EditorID + "\"");
                        stuff.WeaponsToSet.Add(enchanted_item);
                    }
                    else
                    {
                        bool copyAsOverride = false;

                        // Set Value
                        if (enchanted_item.BasicStats == null)
                        {
                            if (item.Value != null)
                            {
                                enchanted_item.BasicStats = new();
                                enchanted_item.BasicStats.Value = (uint)item.Value;
                                copyAsOverride = true;
                            }
                        }
                        else
                        {
                            if (item.Value != null && enchanted_item.BasicStats.Value != item.Value)
                            {
                                enchanted_item.BasicStats.Value = (uint)item.Value;
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
                            Console.WriteLine("Renaming weapon: " + enchanted_item.Name + " to: " + enchanted_item_name);
                            enchanted_item.Name = enchanted_item_name;
                            copyAsOverride = true;
                        }

                        //Set Scripts
                        if (item.SetScripts)
                        {
                            if (enchanted_item.VirtualMachineAdapter == null)
                            {
                                if (itemGetter.VirtualMachineAdapter != null)
                                {
                                    enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();
                                    copyAsOverride = true;
                                }
                            } else if (itemGetter.VirtualMachineAdapter == null)
                            {
                                if (enchanted_item.VirtualMachineAdapter != null)
                                {
                                    enchanted_item.VirtualMachineAdapter = null;
                                    copyAsOverride = true;
                                }
                            } else
                            {
                                var equalsmask = enchanted_item.VirtualMachineAdapter.GetEqualsMask(itemGetter.VirtualMachineAdapter);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                if (!equalsmask.Scripts.Overall)
                                {
                                    enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();
                                    copyAsOverride = true;
                                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                            }
                        }

                        if (copyAsOverride)
                        {
                            enchanted_item.EditorID = enchanted_item_EditorID;
                            Console.WriteLine("Altering " + enchanted_item_EditorID);
                            stuff.WeaponsToSet.Add(enchanted_item);
                        }
                    }

                    // Set Leveled Lists
                    if (enchantmentInfo.LeveledLists != null)
                    {
                        foreach (var leveledlistinfo in enchantmentInfo.LeveledLists)
                        {
                            var leveledlisteditorid = leveledlistinfo.LeveledListPrefix + item.EditorID + leveledlistinfo.LeveledListSuffix;
                            bool leveledListAlreadyExists = true;
                            LeveledItem leveledlist;
                            if (leveledlists.TryGetValue(leveledlisteditorid, out var idk))
                            {
                                leveledlist = idk;
                            }
                            else
                            {
                                leveledlist = GetLeveledList(linkCache, state.PatchMod, leveledlisteditorid, config.CheckExistingGenerated, out var b); // Get leveled list
                                                                                                                                    //leveledlist.Entries?.Clear();
                                leveledListAlreadyExists = b;
                                leveledlists.Add(leveledlisteditorid, leveledlist);
                            }

                            if (leveledListAlreadyExists)
                            {
                                if (leveledlist.Entries == null)
                                {
                                    leveledlist.Entries = new();
                                }

                                bool addtoleveledlist = true;
                                if (leveledlistinfo.Mode == "Remove")
                                {
                                    addtoleveledlist = false;
                                }
                                foreach (var entry in leveledlist.Entries)
                                {
                                    if (entry.Data?.Reference.FormKey == enchanted_item.FormKey)
                                    { // Check if item already exists in leveled list
                                        if (leveledlistinfo.Mode == "Remove")
                                        {
                                            leveledlist.Entries.Remove(entry);
                                            Console.WriteLine("Removing item: " + enchanted_item_EditorID + " from leveledlist: " + leveledlist.EditorID);
                                            stuff.LeveledListsToSet.Add(leveledlist);
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
                                    Console.WriteLine("Adding item: " + enchanted_item_EditorID + " to leveledlist: " + leveledlisteditorid);
                                    stuff.LeveledListsToSet.Add(leveledlist);
                                }
                            }
                            else
                            {
                                if (leveledlistinfo.Mode == "Add")
                                {
                                    AddToLeveledList(leveledlist, enchanted_item.FormKey);
                                    Console.WriteLine("Adding item: " + enchanted_item_EditorID + " to leveledlist: " + leveledlisteditorid);
                                    stuff.LeveledListsToSet.Add(leveledlist);
                                }
                            }
                        }
                    }
                }
            }
            return stuff;
        }

        public static UnsafeThreadStuff GenerateArmor(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Config config, InputThing input, ImmutableLoadOrderLinkCache linkCache)
        {
            UnsafeThreadStuff stuff = new();
            foreach (var item in input.Armors.Values)
            {
                Console.WriteLine("Processing Armor: " + item.EditorID);
                IArmorGetter itemGetter;
                if (state.LinkCache.TryResolve<IArmorGetter>(FormKey.Factory(item.FormKey), out var output))
                { // Get template item
                    itemGetter = output;
                }
                else
                {
                    throw new Exception("Could not find Armor:" + item.EditorID + " : " + item.FormKey);
                }

                Dictionary<String, LeveledItem> leveledlists = new();

                foreach (var enchantmentInfo in input.Enchantments.Values)
                {
                    var enchanted_item_EditorID = "Ench_" + item.EditorID + "_" + enchantmentInfo.EditorID;
                    var enchanted_item = GetEnchantedArmor(linkCache, state.PatchMod, enchanted_item_EditorID, config.CheckExistingGenerated, out var EnchantedItemAlreadyExists, enchantmentInfo.Enchantment.AsNullableLink(), itemGetter.AsNullableLink());
                    var enchanted_item_name = enchantmentInfo.Prefix + itemGetter.Name + enchantmentInfo.Suffix;

                    if (!EnchantedItemAlreadyExists)
                    { // Create Enchanted Item

                        enchanted_item.Name = enchanted_item_name;
                        enchanted_item.EnchantmentAmount = enchantmentInfo.EnchantmentAmount;

                        // Set Value
                        if (item.Value != null)
                        {
                            enchanted_item.Value = (uint)item.Value;
                        }
                        else
                        {
                            enchanted_item.Value = itemGetter.Value;
                        }

                        Console.WriteLine("Generating armor \"" + enchanted_item_EditorID + "\"");
                        stuff.ArmorToSet.Add(enchanted_item);
                    }
                    else
                    {
                        bool copyAsOverride = false;

                        // Set Value
                        if (item.Value != null)
                        {
                            if (enchanted_item.Value != item.Value)
                            {
                                enchanted_item.Value = (uint)item.Value;
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
                        if (enchanted_item.Name != null && enchanted_item.Name.ToString() != enchanted_item_name)
                        {
                            Console.WriteLine("Renaming armor: " + enchanted_item.Name.ToString() + " to: " + enchanted_item_name);
                            enchanted_item.Name = enchanted_item_name;
                            copyAsOverride = true;
                        }


                        //Set Scripts
                        if (item.SetScripts)
                        {
                            if (enchanted_item.VirtualMachineAdapter == null)
                            {
                                if (itemGetter.VirtualMachineAdapter != null)
                                {
                                    enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();
                                    copyAsOverride = true;
                                }
                            }
                            else if (itemGetter.VirtualMachineAdapter == null)
                            {
                                if (enchanted_item.VirtualMachineAdapter != null)
                                {
                                    enchanted_item.VirtualMachineAdapter = null;
                                    copyAsOverride = true;
                                }
                            }
                            else
                            {
                                var equalsmask = enchanted_item.VirtualMachineAdapter.GetEqualsMask(itemGetter.VirtualMachineAdapter);
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                if (!equalsmask.Scripts.Overall)
                                {
                                    enchanted_item.VirtualMachineAdapter = itemGetter.VirtualMachineAdapter.DeepCopy();
                                    copyAsOverride = true;
                                }
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                            }
                        }

                        if (copyAsOverride)
                        {
                            enchanted_item.EditorID = enchanted_item_EditorID;
                            Console.WriteLine("Altering " + enchanted_item_EditorID);
                            stuff.ArmorToSet.Add(enchanted_item);
                        }
                    }

                    // Set Leveled Lists
                    if (enchantmentInfo.LeveledLists != null)
                    {
                        foreach (var leveledlistinfo in enchantmentInfo.LeveledLists)
                        {
                            var leveledlisteditorid = leveledlistinfo.LeveledListPrefix + item.EditorID + leveledlistinfo.LeveledListSuffix;
                            bool leveledListAlreadyExists = true;
                            LeveledItem leveledlist;
                            if (leveledlists.TryGetValue(leveledlisteditorid, out var idk))
                            {
                                leveledlist = idk;
                            }
                            else
                            {
                                leveledlist = GetLeveledList(linkCache, state.PatchMod, leveledlisteditorid, config.CheckExistingGenerated, out var b); // Get leveled list
                                                                                                                                                        //leveledlist.Entries?.Clear();
                                leveledListAlreadyExists = b;
                                leveledlists.Add(leveledlisteditorid, leveledlist);
                            }

                            if (leveledListAlreadyExists)
                            {
                                if (leveledlist.Entries == null)
                                {
                                    leveledlist.Entries = new();
                                }

                                bool addtoleveledlist = true;
                                if (leveledlistinfo.Mode == "Remove")
                                {
                                    addtoleveledlist = false;
                                }
                                foreach (var entry in leveledlist.Entries)
                                {
                                    if (entry.Data?.Reference.FormKey == enchanted_item.FormKey)
                                    { // Check if item already exists in leveled list
                                        if (leveledlistinfo.Mode == "Remove")
                                        {
                                            leveledlist.Entries.Remove(entry);
                                            Console.WriteLine("Removing item: " + enchanted_item_EditorID + " from leveledlist: " + leveledlist.EditorID);
                                            stuff.LeveledListsToSet.Add(leveledlist);
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
                                    Console.WriteLine("Adding item: " + enchanted_item_EditorID + " to leveledlist: " + leveledlisteditorid);
                                    stuff.LeveledListsToSet.Add(leveledlist);
                                }
                            }
                            else
                            {
                                if (leveledlistinfo.Mode == "Add")
                                {
                                    AddToLeveledList(leveledlist, enchanted_item.FormKey);
                                    Console.WriteLine("Adding item: " + enchanted_item_EditorID + " to leveledlist: " + leveledlisteditorid);
                                    stuff.LeveledListsToSet.Add(leveledlist);
                                }
                            }
                        }
                    }
                }
            }
            return stuff;
        }

        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "generatedenchantments.esp")
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
            List<InputThing> inputs = GetInputs(state, enabledMods);


            var linkcache = state.LoadOrder.ToImmutableLinkCache();

            List<Task<UnsafeThreadStuff>> tasks = new();
            foreach (var input in inputs)
            {
                tasks.Add(Task<UnsafeThreadStuff>.Factory.StartNew(() => GenerateArmor(state, config, input, linkcache)));
                tasks.Add(Task<UnsafeThreadStuff>.Factory.StartNew(() => GenerateWeapon(state, config, input, linkcache)));
            }
            Task.WaitAll(tasks.ToArray());
            Console.WriteLine("Finishing Up!");
            foreach(var task in tasks)
            {
                foreach(var weapon in task.Result.WeaponsToSet)
                {
                    state.PatchMod.Weapons.Set(weapon);
                }
                foreach (var armor in task.Result.ArmorToSet)
                {
                    state.PatchMod.Armors.Set(armor);
                }
                foreach (var leveledlist in task.Result.LeveledListsToSet)
                {
                    state.PatchMod.LeveledItems.Set(leveledlist);
                }
            }
        } // End of Patching
    }
}