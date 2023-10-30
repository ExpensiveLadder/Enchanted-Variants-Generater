﻿using Mutagen.Bethesda.Plugins.Cache.Internals.Implementations;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Mutagen.Bethesda.Plugins.Binary.Processing.BinaryFileProcessor;

namespace EnchantedVariantsGenerater
{
    /*
    internal class Dump
    {
    }

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


    public class InputThing
    {
        //                EditorID, Info
        public Dictionary<string, EnchantmentInfo> Enchantments { get; } = new();
        public Dictionary<string, ItemJSON> Weapons { get; } = new();
        public Dictionary<string, ItemJSON> Armors { get; } = new();
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

    public static LeveledItem GetLeveledList(ImmutableLoadOrderLinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, ISkyrimMod patchMod, string editorID, bool checkExistingGenerated, out bool alreadyExists)
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
            leveledlist = new LeveledItem(patchMod, editorID);
            leveledlist.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
            leveledlist.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
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

    public static Weapon GetEnchantedWeapon(ImmutableLoadOrderLinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, ISkyrimMod patchMod, string editorID, bool checkExistingGenerated, out bool alreadyExists, IFormLinkNullable<IObjectEffectGetter> enchantment, IFormLinkNullable<IWeaponGetter> template)
    {
        Weapon weapon;

        if (checkExistingGenerated && linkCache.TryResolve<IWeaponGetter>(editorID, out var weapon_Original))
        { // Get Enchanted Weapon if it already exists
            alreadyExists = true;
            weapon = weapon_Original.DeepCopy(weapontranslationmark);
        }
        else
        { // Create Enchanted Weapon
            alreadyExists = false;
            weapon = new Weapon(patchMod, editorID)
            {
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

    public static Armor GetEnchantedArmor(ImmutableLoadOrderLinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, ISkyrimMod patchMod, string editorID, bool checkExistingGenerated, out bool alreadyExists, IFormLinkNullable<IObjectEffectGetter> enchantment, IFormLinkNullable<IArmorGetter> template)
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
            armor = new Armor(patchMod, editorID)
            {
                EditorID = editorID,
                ObjectEffect = enchantment,
                TemplateArmor = template
            };
        }
        return armor;
    }

    public static UnsafeThreadStuff GenerateWeapon(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Config config, InputThing input, ImmutableLoadOrderLinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
    {
        UnsafeThreadStuff stuff = new();
        foreach (var item in input.Weapons.Values)
        {
            if (config.VerboseLogging) Console.WriteLine("Processing Weapon: " + item.EditorID);
            IWeaponGetter itemGetter;
            if (linkCache.TryResolve<IWeaponGetter>(FormKey.Factory(item.FormKey), out var output))
            { // Get template item
                itemGetter = output;
            }
            else
            {
                throw new Exception("Could not find Weapon:" + item.EditorID + " : " + item.FormKey);
            }

            Dictionary<string, LeveledItem> leveledlists = new();

            foreach (var enchantmentInfo in input.Enchantments.Values)
            {
                var enchanted_item_EditorID = "Ench_" + item.EditorID + "_" + enchantmentInfo.EditorID;
                var enchanted_item = GetEnchantedWeapon(linkCache, state.PatchMod, enchanted_item_EditorID, config.CheckExistingGenerated, out var EnchantedItemAlreadyExists, enchantmentInfo.Enchantment.ToNullableLink(), itemGetter.ToNullableLink());
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

    public static UnsafeThreadStuff GenerateArmor(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, Config config, InputThing input, ImmutableLoadOrderLinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
    {
        UnsafeThreadStuff stuff = new();
        foreach (var item in input.Armors.Values)
        {
            if (config.VerboseLogging) Console.WriteLine("Processing Armor: " + item.EditorID);
            IArmorGetter itemGetter;
            if (state.LinkCache.TryResolve<IArmorGetter>(FormKey.Factory(item.FormKey), out var output))
            { // Get template item
                itemGetter = output;
            }
            else
            {
                throw new Exception("Could not find Armor:" + item.EditorID + " : " + item.FormKey);
            }

            Dictionary<string, LeveledItem> leveledlists = new();

            foreach (var enchantmentInfo in input.Enchantments.Values)
            {
                var enchanted_item_EditorID = "Ench_" + item.EditorID + "_" + enchantmentInfo.EditorID;
                var enchanted_item = GetEnchantedArmor(linkCache, state.PatchMod, enchanted_item_EditorID, config.CheckExistingGenerated, out var EnchantedItemAlreadyExists, enchantmentInfo.Enchantment.ToNullableLink(), itemGetter.ToNullableLink());
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
    */
}
