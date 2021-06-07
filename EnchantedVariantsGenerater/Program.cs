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
    }


    public class Program
    {

        public static LeveledItemEntry CreateLeveledItemEntry(short level)
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

        public static LeveledItemEntry CreateLeveledItemEntry(short level, IFormLink<IItemGetter> reference)
        {
            return new LeveledItemEntry
            {
                Data = new LeveledItemEntryData()
                {
                    Count = 1,
                    Level = level,
                    Reference = reference
                }
            };
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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
        public static EnchantmentInfo ParseEnchantments(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, EnchantmentJSON enchantmentJSON)
        {

            IObjectEffectGetter enchantment;

            if (enchantmentJSON.FormKey != null)
            {
                enchantment = state.LinkCache.Resolve<IObjectEffectGetter>(FormKey.Factory(enchantmentJSON.FormKey));
            }
            else if (enchantmentJSON.EditorID != null)
            {
                enchantment = state.LinkCache.Resolve<IObjectEffectGetter>(enchantmentJSON.EditorID);
            }
            else
            {
                Console.WriteLine("ERROR: enchantment does not have a formkey or editorID specified");
                throw new Exception();
            }

            short level = 1;
            if (enchantmentJSON.Level != null) level = (short)enchantmentJSON.Level;

            var enchantmentGetter = new EnchantmentInfo
            {
                Level = level,
                EnchantmentAmount = (ushort?)enchantmentJSON.EnchantmentAmount,
                Enchantment = enchantment,
                Prefix = enchantmentJSON.Prefix,
                Suffix = enchantmentJSON.Suffix,
                Sublist = enchantmentJSON.Sublist,
                EditorID = enchantment.EditorID
            };

            return enchantmentGetter;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
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


            foreach (var filePath in Directory.GetFiles(state.ExtraSettingsDataPath, "*.json"))
            {
                Console.WriteLine("MESSAGE: Reading JSON file \"" + filePath + "\"");
                var inputJSON = JsonConvert.DeserializeObject<InputFileJSON>(File.ReadAllText(filePath));
                if (inputJSON == null) continue;
                if (inputJSON.InputWeapons != null) inputWeaponsJSON.Add(inputJSON.InputWeapons);
                if (inputJSON.InputArmors != null) inputArmorsJSONs.Add(inputJSON.InputArmors);
            }

            // Create base sublist
            var baseSublist = new LeveledItem(state.PatchMod);
            baseSublist.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
            baseSublist.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
            // baseSublist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();

            // Weapons Generator
            foreach (var inputWeapons in inputWeaponsJSON)
            {
                if (inputWeapons.Enchantments == null)
                {
                    Console.WriteLine("ERROR: Weapon enchantment list is empty");
                    continue;
                }

                // Get Weapon FormKeys
                List<FormKey> weaponFormkeys = new();

                if (inputWeapons.WeaponFormKeys != null)
                {
                    foreach (var formKeyString in inputWeapons.WeaponFormKeys)
                    {
                        if (FormKey.TryFactory(formKeyString, out var formKey))
                        {
                            weaponFormkeys.Add(formKey);
                        }
                        else
                        {
                            Console.WriteLine("ERROR: could not resolve weapon formkey \"" + formKeyString + "\"");
                        }
                    }
                }

                if (inputWeapons.WeaponEditorIDs != null)
                {
                    foreach (var formIDString in inputWeapons.WeaponEditorIDs)
                    {
                        weaponFormkeys.Add(state.LinkCache.Resolve<IWeaponGetter>(formIDString).FormKey);
                    }
                }


                // Parse Enchantments
                List<EnchantmentInfo> enchantmentInfos = new();

                foreach (var enchantmentGetter in inputWeapons.Enchantments)
                {
                    enchantmentInfos.Add(ParseEnchantments(state, enchantmentGetter));
                }

                // Generate Enchanted Weapons
                foreach (var weaponFormkey in weaponFormkeys)
                {
                    var weaponGetter = state.LinkCache.Resolve<IWeaponGetter>(weaponFormkey); // Get base weapon

                    // Create Leveled Lists

                    var LItemEnchWeaponAll = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                    LItemEnchWeaponAll.EditorID = "LItemEnch_" + weaponGetter.EditorID + "_All";
                    LItemEnchWeaponAll.Entries = new Noggog.ExtendedList<LeveledItemEntry>();

                    var LItemEnchWeaponSublists = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                    LItemEnchWeaponSublists.EditorID = "LItemEnch_" + weaponGetter.EditorID + "_Sublists";
                    LItemEnchWeaponSublists.Entries = new Noggog.ExtendedList<LeveledItemEntry>();

                    var LItemEnchWeaponSublistsBest = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(LItemEnchWeaponSublists);
                    LItemEnchWeaponSublistsBest.EditorID += "Best";
                    LItemEnchWeaponSublistsBest.Entries = new Noggog.ExtendedList<LeveledItemEntry>();

                    var sublists = new Dictionary<string, LeveledItem>();
                    var sublists_best = new Dictionary<string, LeveledItem>();

                    foreach (var enchantmentInfo in enchantmentInfos)
                    {
                        var weapon = state.PatchMod.Weapons.DuplicateInAsNewRecord(weaponGetter); // Create new weapon and add it to patch
                        weapon.ObjectEffect.SetTo(enchantmentInfo.Enchantment); // Set enchantment to weapon
                        weapon.Template.SetTo(weaponFormkey); // Set template to base weapon
                        if (enchantmentInfo.EnchantmentAmount != null) weapon.EnchantmentAmount = enchantmentInfo.EnchantmentAmount; // Set enchantment amount, if null it will have unlimited charges
                        weapon.EditorID = "Ench_" + weapon.EditorID + "_" + enchantmentInfo.EditorID?.Replace("Ench", "");
                        weapon.Name = enchantmentInfo.Prefix + weapon.Name + enchantmentInfo.Suffix;

                        // Generate Leveled Lists


                        // Add to Leveled List All
                        var leveledItemEntry_LItemEnchWeaponAll = CreateLeveledItemEntry(enchantmentInfo.Level, weapon.FormKey);
                        LItemEnchWeaponAll.Entries.Add(leveledItemEntry_LItemEnchWeaponAll);


                        // Need to redo everything below this

                        // Sublist

                        if (enchantmentInfo.Sublist != null)
                        {

                            if (!sublists.TryGetValue(enchantmentInfo.Sublist, out LeveledItem? sublist))
                            {
                                sublist = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                                sublist.EditorID = "SublistEnch_" + weaponGetter.EditorID + "_" + enchantmentInfo.Sublist;
                                sublists.Add(enchantmentInfo.Sublist, sublist);

                                // Leveled List of Sublists

                                var sublistsEntry = CreateLeveledItemEntry(enchantmentInfo.Level);
                                sublistsEntry.Data?.Reference.SetTo(sublist);

                                if (LItemEnchWeaponSublists.Entries == null) LItemEnchWeaponSublists.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                                LItemEnchWeaponSublists.Entries.Add(sublistsEntry);
                            };
                            if (sublist.Entries == null) sublist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                            var leveledItemEntrySublist = CreateLeveledItemEntry(enchantmentInfo.Level, weapon.FormKey);
                            sublist.Entries.Add(leveledItemEntrySublist);

                            state.PatchMod.LeveledItems.Set(sublist);


                            // Sublists Best
                            if (!sublists_best.TryGetValue(enchantmentInfo.Sublist, out LeveledItem? sublist_best))
                            {
                                sublist_best = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                                sublist_best.EditorID = "SublistEnchBest_" + weaponGetter.EditorID + "_" + enchantmentInfo.Sublist;
                                sublist_best.Flags -= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                                sublist_best.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                                sublists_best.Add(enchantmentInfo.Sublist, sublist_best);

                                // Leveled List of Sublists

                                var sublistsEntry = CreateLeveledItemEntry(enchantmentInfo.Level);
                                sublistsEntry.Data?.Reference.SetTo(sublist_best);

                                if (LItemEnchWeaponSublistsBest.Entries == null) LItemEnchWeaponSublistsBest.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                                LItemEnchWeaponSublistsBest.Entries.Add(sublistsEntry);
                            };
                            if (sublist_best.Entries == null) sublist_best.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                            var leveledItemEntrySublistBest = CreateLeveledItemEntry(enchantmentInfo.Level, weapon.FormKey);
                            sublist_best.Entries.Add(leveledItemEntrySublistBest);

                            state.PatchMod.LeveledItems.Set(sublist_best);
                        }
                    }
                }

            }
        } // End of Patching
    }
}
