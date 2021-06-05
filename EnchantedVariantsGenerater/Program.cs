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
    public class Program
    {

        public static LeveledItemEntry GetLeveledItemEntry(short level)
        {
            return new LeveledItemEntry
            {
                Data = new LeveledItemEntryData()
                {
                    Count = 1,
                    Level = level
                }
            };
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "enchantments.esp")
                .Run(args);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {

            var enchantments_json = JsonConvert.DeserializeObject<EnchantmentsJSON>(File.ReadAllText(Path.Combine(state.ExtraSettingsDataPath, "enchantments.json")));
            if (enchantments_json?.Enchantments == null) throw new Exception("Could not get enchantments.json");

            var weapons_json = JsonConvert.DeserializeObject<WeaponsJSON>(File.ReadAllText(Path.Combine(state.ExtraSettingsDataPath, "weapons.json")));
            if (weapons_json?.Weapons == null) throw new Exception("Could not get weapons.json");

            var baseSublist = new LeveledItem(state.PatchMod);
            baseSublist.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
            baseSublist.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
            baseSublist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();

            foreach (var weaponEditorID in weapons_json.Weapons) {

                var weaponBase = state.LinkCache.Resolve<IWeaponGetter>(weaponEditorID);
                FormKey weaponFormKey = weaponBase.FormKey;

                var weapon_name = weaponBase.Name?.String;
                if (weapon_name == null) throw new Exception("Weapon's name is null");

                var LItemEnchWeaponAll = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                LItemEnchWeaponAll.EditorID = "LItemEnch_" + weaponBase.EditorID + "_All";

                var LItemEnchWeaponSublists = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                LItemEnchWeaponSublists.EditorID = "LItemEnch_" + weaponBase.EditorID + "_Sublists";

                var LItemEnchWeaponSublistsBest = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(LItemEnchWeaponSublists);
                LItemEnchWeaponSublistsBest.EditorID += "Best";

                var sublists = new Dictionary<string, LeveledItem>();
                var sublists_best = new Dictionary<string, LeveledItem>();

                foreach (var enchantmentJSON in enchantments_json.Enchantments) {


                    FormKey enchantmentFormKey;
                    IObjectEffectGetter enchantment;
                    if (enchantmentJSON.FormID != null)
                    {
                        enchantmentFormKey = FormKey.Factory(enchantmentJSON.FormID);
                        enchantment = state.LinkCache.Resolve<IObjectEffectGetter>(enchantmentFormKey);
                    }
                    else
                    {
                        if (enchantmentJSON.EditorID != null)
                        {
                            enchantment = state.LinkCache.Resolve<IObjectEffectGetter>(enchantmentJSON.EditorID);
                            enchantmentFormKey = enchantment.FormKey;
                        }
                        else throw new ArgumentException("Enchantment in JSON does not have FormKey or EditorID specified");
                    }

                    var weapon = state.PatchMod.Weapons.DuplicateInAsNewRecord(weaponBase);

                    if (enchantment.EditorID == null) {
                        Console.WriteLine("Enchantment \""+enchantmentFormKey+"\" has a null EditorID");
                        weapon.EditorID = "Ench_" + weaponBase.EditorID + "_" + enchantment.Name;
                    } else {
                        weapon.EditorID = "Ench_" + weaponBase.EditorID + "_" + enchantment.EditorID.Replace("Ench", "");
                    }

                    weapon.Template.SetTo(weaponFormKey);
                    if (enchantmentJSON.Suffix != null) weapon.Name = weapon_name + enchantmentJSON.Suffix;
                    weapon.ObjectEffect.SetTo(enchantmentFormKey);
                    weapon.EnchantmentAmount = (ushort?)enchantmentJSON.EnchantmentAmount;

                    // Leveled Lists

                    short level = 1;
                    if (enchantmentJSON.Level != null) level = (short)enchantmentJSON.Level;

                    var leveledItemEntryLItemEnchWeaponAll = GetLeveledItemEntry(level);
                    leveledItemEntryLItemEnchWeaponAll.Data?.Reference.SetTo(weapon);

                    // Add to Leveled List All
                    if (LItemEnchWeaponAll.Entries == null) LItemEnchWeaponAll.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                    LItemEnchWeaponAll.Entries.Add(leveledItemEntryLItemEnchWeaponAll);

                    // Sublist

                    if (enchantmentJSON.Sublist != null)
                    {

                        if (!sublists.TryGetValue(enchantmentJSON.Sublist, out LeveledItem? sublist))
                        {
                            sublist = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                            sublist.EditorID = "SublistEnch_" + weaponBase.EditorID + "_" + enchantmentJSON.Sublist;
                            sublists.Add(enchantmentJSON.Sublist, sublist);

                            // Leveled List of Sublists

                            var sublistsEntry = GetLeveledItemEntry(level);
                            sublistsEntry.Data?.Reference.SetTo(sublist);

                            if (LItemEnchWeaponSublists.Entries == null) LItemEnchWeaponSublists.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                            LItemEnchWeaponSublists.Entries.Add(sublistsEntry);
                        };
                        if (sublist.Entries == null) sublist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                        var leveledItemEntrySublist = GetLeveledItemEntry(level);
                        leveledItemEntrySublist.Data?.Reference.SetTo(weapon);
                        sublist.Entries.Add(leveledItemEntrySublist);

                        state.PatchMod.LeveledItems.Set(sublist);


                        // Sublists Best
                        if (!sublists_best.TryGetValue(enchantmentJSON.Sublist, out LeveledItem? sublist_best))
                        {
                            sublist_best = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                            sublist_best.EditorID = "SublistEnchBest_" + weaponBase.EditorID + "_" + enchantmentJSON.Sublist;
                            sublist_best.Flags -= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                            sublists_best.Add(enchantmentJSON.Sublist, sublist_best);

                            // Leveled List of Sublists

                            var sublistsEntry = GetLeveledItemEntry(level);
                            sublistsEntry.Data?.Reference.SetTo(sublist_best);

                            if (LItemEnchWeaponSublistsBest.Entries == null) LItemEnchWeaponSublistsBest.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                            LItemEnchWeaponSublistsBest.Entries.Add(sublistsEntry);
                        };
                        if (sublist_best.Entries == null) sublist_best.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                        var leveledItemEntrySublistBest = GetLeveledItemEntry(level);
                        leveledItemEntrySublistBest.Data?.Reference.SetTo(weapon);
                        sublist_best.Entries.Add(leveledItemEntrySublistBest);

                        state.PatchMod.LeveledItems.Set(sublist_best);
                    }
                }

                /*
                if (LItemEnchWeaponSublists.Entries != null)
                {
                    var LItemEnchWeaponBoss = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(LItemEnchWeaponSublists);
                    LItemEnchWeaponBoss.EditorID = LItemEnchWeaponSublists.EditorID.Replace("Sublists", "Boss");
                }
                */

                /*
                // Sublists Best
                var sublists_best = new List<LeveledItem>();
                foreach (var sublist in sublists.Values)
                {
                    if (sublist.Entries == null) continue;

                    var sublist_best = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(sublist);
                    sublist_best.EditorID += "_Best";
                    sublist_best.Flags -= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;

                    sublists_best.Add(sublists_best);
                }

                var LItemEnchWeaponSublistsBest = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                LItemEnchWeaponSublistsBest.EditorID = "LItemEnch_" + weaponBase.EditorID + "_SublistsBest";
                if (LItemEnchWeaponSublistsBest.Entries == null) LItemEnchWeaponSublistsBest.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                foreach (var sublistss in sublists_best)
                {
                    short level = 1;
                    if (sublistss.Entries != null) {
                        foreach (var sublistEntry in sublistss.Entries)
                        {
                            if (sublistEntry.Data != null) level = sublistEntry.Data.Level;
                            break;
                        }
                    }

                    var itemEntry = GetLeveledItemEntry(level);
                    if (itemEntry.Data == null) itemEntry.Data = new LeveledItemEntryData();
                    itemEntry.Data.Reference.SetTo(sublistss);
                    LItemEnchWeaponSublistsBest.Entries.Add(itemEntry);
                }
                state.PatchMod.LeveledItems.Set(LItemEnchWeaponSublistsBest);
                */
            }
        }
    }
}
