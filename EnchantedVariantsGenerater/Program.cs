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
                throw new Exception("ERROR: enchantment does not have a formkey or editorID specified");
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
                EditorID = enchantment.EditorID,
                Boss = enchantmentJSON.Boss,
                Create_LItemEnchWeaponAll = enchantmentJSON.Crea
            };

            return enchantmentGetter;
        }

        public static LeveledItem CreateLItemEnchWeapon(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, IMajorRecordGetter weaponGetter, string suffix)
        {
            LeveledItem LItemEnchWeapon;

            string LItemEnchWeapon_EditorID = "LItemEnch_" + weaponGetter.EditorID + "_" + suffix;
            if (state.LinkCache.TryResolve<ILeveledItemGetter>(LItemEnchWeapon_EditorID, out var LItemEnchWeapon_Original))
            {
                Console.WriteLine("Leveled List \"" + LItemEnchWeapon_EditorID + "\" already exists in plugin \"" + LItemEnchWeapon_Original.FormKey.ModKey.ToString() + "\", copying as override, and appending changes");
                LItemEnchWeapon = LItemEnchWeapon_Original.DeepCopy();
                state.PatchMod.LeveledItems.Set(LItemEnchWeapon);
            }
            else
            {
                LItemEnchWeapon = new LeveledItem(state.PatchMod);
                LItemEnchWeapon.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
                LItemEnchWeapon.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                LItemEnchWeapon.EditorID = LItemEnchWeapon_EditorID;
                LItemEnchWeapon.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
            }
            return LItemEnchWeapon;
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

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            // Read JSON Files
            List<InputWeaponsJSON> inputWeaponsJSON = new();
            List<InputArmorsJSON> inputArmorsJSONs = new();


            foreach (var filePath in Directory.GetFiles(state.ExtraSettingsDataPath, "*.json"))
            {
                Console.WriteLine("Reading JSON file \"" + filePath + "\"");
                var inputJSON = JsonConvert.DeserializeObject<InputFileJSON>(File.ReadAllText(filePath));
                if (inputJSON == null) continue;
                if (inputJSON.InputWeapons != null) inputWeaponsJSON.Add(inputJSON.InputWeapons);
                if (inputJSON.InputArmors != null) inputArmorsJSONs.Add(inputJSON.InputArmors);
            }

            // Create base sublist
            var baseSublist = new LeveledItem(state.PatchMod);
            baseSublist.Flags |= LeveledItem.Flag.CalculateForEachItemInCount;
            baseSublist.Flags |= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;

            // Weapons Generator
            foreach (var inputWeapons in inputWeaponsJSON)
            {
                if (inputWeapons.Enchantments == null)
                {
                    Console.WriteLine("Error: Weapon enchantment list is empty");
                    continue;
                }

                // Get Weapon FormKeys
                List<FormKey> weaponFormkeys = new();

                if (inputWeapons.WeaponFormKeys != null)
                {
                    foreach (var formKeyString in inputWeapons.WeaponFormKeys)
                    {
                        weaponFormkeys.Add(FormKey.Factory(formKeyString));
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

                    LeveledItem LItemEnchWeaponAll = CreateLItemEnchWeapon(state, weaponGetter, "All");
             
                    LeveledItem LItemEnchWeaponAllUnleveled = CreateLItemEnchWeapon(state, weaponGetter, "AllUnleveled");
                    LeveledItem LItemEnchWeaponSublists = CreateLItemEnchWeapon(state, weaponGetter, "Sublists");
                    LeveledItem LItemEnchWeaponSublistsBest = CreateLItemEnchWeapon(state, weaponGetter, "SublistsBest");
                    LeveledItem LItemEnchWeaponBoss = CreateLItemEnchWeapon(state, weaponGetter, "Boss");

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

                        GetLeveledItemEntries(LItemEnchWeaponAll).Add(CreateLeveledItemEntry(enchantmentInfo.Level, weapon.FormKey)); // Add to Leveled List All
                        GetLeveledItemEntries(LItemEnchWeaponAllUnleveled).Add(CreateLeveledItemEntry(1, weapon.FormKey)); // Add to Leveled List All Unleveled

                        // Sublist
                        if (enchantmentInfo.Sublist != null)
                        {

                            if (!sublists.TryGetValue(enchantmentInfo.Sublist, out LeveledItem? sublist)) // If Sublist does not exist, create it
                            {
                                sublist = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                                sublist.EditorID = "SublistEnch_" + weaponGetter.EditorID + "_" + enchantmentInfo.Sublist;
                                sublists.Add(enchantmentInfo.Sublist, sublist);

                                // Leveled List of Sublists
                                var leveledItemEntry = CreateLeveledItemEntry(enchantmentInfo.Level, sublist);
                                GetLeveledItemEntries(LItemEnchWeaponSublists).Add(leveledItemEntry);

                                if (enchantmentInfo.Boss)
                                {
                                    GetLeveledItemEntries(LItemEnchWeaponBoss).Add(leveledItemEntry);
                                }
                            };
                            if (sublist.Entries == null) sublist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                            sublist.Entries.Add(CreateLeveledItemEntry(enchantmentInfo.Level, weapon.FormKey));

                            state.PatchMod.LeveledItems.Set(sublist);


                            // Sublists Best
                            if (!sublists_best.TryGetValue(enchantmentInfo.Sublist, out LeveledItem? sublist_best)) // If Sublist does not exist, create it
                            {
                                sublist_best = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                                sublist_best.EditorID = "SublistEnchBest_" + weaponGetter.EditorID + "_" + enchantmentInfo.Sublist;
                                sublist_best.Flags -= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                                sublists_best.Add(enchantmentInfo.Sublist, sublist_best);

                                // Leveled List of Sublists
                                GetLeveledItemEntries(LItemEnchWeaponSublistsBest).Add(CreateLeveledItemEntry(enchantmentInfo.Level, sublist_best));
                            };
                            if (sublist_best.Entries == null) sublist_best.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                            sublist_best.Entries.Add(CreateLeveledItemEntry(enchantmentInfo.Level, weapon.FormKey));

                            state.PatchMod.LeveledItems.Set(sublist_best);
                        }
                    }
                }

            }

            // Armors Generator
            foreach (var inputArmors in inputArmorsJSONs)
            {
                if (inputArmors.Enchantments == null)
                {
                    Console.WriteLine("Error: Armor enchantment list is empty");
                    continue;
                }

                // Get Weapon FormKeys
                List<FormKey> armorFormKeys = new();

                if (inputArmors.ArmorFormKeys != null)
                {
                    foreach (var formKeyString in inputArmors.ArmorFormKeys)
                    {
                        armorFormKeys.Add(FormKey.Factory(formKeyString));
                    }
                }

                if (inputArmors.ArmorEditorIDs != null)
                {
                    foreach (var formIDString in inputArmors.ArmorEditorIDs)
                    {
                        armorFormKeys.Add(state.LinkCache.Resolve<IArmorGetter>(formIDString).FormKey);
                    }
                }


                // Parse Enchantments
                List<EnchantmentInfo> enchantmentInfos = new();
                foreach (var enchantmentGetter in inputArmors.Enchantments)
                {
                    enchantmentInfos.Add(ParseEnchantments(state, enchantmentGetter));
                }

                // Generate Enchanted Armors
                foreach (var armorFormKey in armorFormKeys)
                {
                    var armorGetter = state.LinkCache.Resolve<IArmorGetter>(armorFormKey); // Get base armor

                    // Create Leveled Lists

                    LeveledItem LItemEnchArmorAll = CreateLItemEnchWeapon(state, armorGetter, "All");
                    LeveledItem LItemEnchArmorAllUnleveled = CreateLItemEnchWeapon(state, armorGetter, "AllUnleveled");
                    LeveledItem LItemEnchArmorSublists = CreateLItemEnchWeapon(state, armorGetter, "Sublists");
                    LeveledItem LItemEnchArmorSublistsBest = CreateLItemEnchWeapon(state, armorGetter, "SublistsBest");
                    LeveledItem LItemEnchArmorBoss = CreateLItemEnchWeapon(state, armorGetter, "Boss");

                    var sublists = new Dictionary<string, LeveledItem>();
                    var sublists_best = new Dictionary<string, LeveledItem>();

                    foreach (var enchantmentInfo in enchantmentInfos)
                    {
                        var armor = state.PatchMod.Armors.DuplicateInAsNewRecord(armorGetter); // Create new weapon and add it to patch
                        armor.ObjectEffect.SetTo(enchantmentInfo.Enchantment); // Set enchantment to weapon
                        armor.TemplateArmor.SetTo(armorFormKey); // Set template to base weapon
                        armor.EditorID = "Ench_" + armor.EditorID + "_" + enchantmentInfo.EditorID?.Replace("Ench", "");
                        armor.Name = enchantmentInfo.Prefix + armor.Name + enchantmentInfo.Suffix;

                        // Generate Leveled Lists

                        GetLeveledItemEntries(LItemEnchArmorAll).Add(CreateLeveledItemEntry(enchantmentInfo.Level, armor.FormKey)); // Add to Leveled List All
                        GetLeveledItemEntries(LItemEnchArmorAllUnleveled).Add(CreateLeveledItemEntry(1, armor.FormKey)); // Add to Leveled List All Unleveled

                        // Sublist
                        if (enchantmentInfo.Sublist != null)
                        {

                            if (!sublists.TryGetValue(enchantmentInfo.Sublist, out LeveledItem? sublist)) // If Sublist does not exist, create it
                            {
                                sublist = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                                sublist.EditorID = "SublistEnch_" + armorGetter.EditorID + "_" + enchantmentInfo.Sublist;
                                sublists.Add(enchantmentInfo.Sublist, sublist);

                                // Leveled List of Sublists
                                var leveledItemEntry = CreateLeveledItemEntry(enchantmentInfo.Level, sublist);
                                GetLeveledItemEntries(LItemEnchArmorSublists).Add(leveledItemEntry);

                                if (enchantmentInfo.Boss)
                                {
                                    GetLeveledItemEntries(LItemEnchArmorBoss).Add(leveledItemEntry);
                                }
                            };
                            if (sublist.Entries == null) sublist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                            sublist.Entries.Add(CreateLeveledItemEntry(enchantmentInfo.Level, armor.FormKey));

                            state.PatchMod.LeveledItems.Set(sublist);


                            // Sublists Best
                            if (!sublists_best.TryGetValue(enchantmentInfo.Sublist, out LeveledItem? sublist_best)) // If Sublist does not exist, create it
                            {
                                sublist_best = state.PatchMod.LeveledItems.DuplicateInAsNewRecord(baseSublist);
                                sublist_best.EditorID = "SublistEnchBest_" + armorGetter.EditorID + "_" + enchantmentInfo.Sublist;
                                sublist_best.Flags -= LeveledItem.Flag.CalculateFromAllLevelsLessThanOrEqualPlayer;
                                sublists_best.Add(enchantmentInfo.Sublist, sublist_best);

                                // Leveled List of Sublists
                                GetLeveledItemEntries(LItemEnchArmorSublistsBest).Add(CreateLeveledItemEntry(enchantmentInfo.Level, sublist_best));
                            };
                            if (sublist_best.Entries == null) sublist_best.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                            sublist_best.Entries.Add(CreateLeveledItemEntry(enchantmentInfo.Level, armor.FormKey));

                            state.PatchMod.LeveledItems.Set(sublist_best);
                        }
                    }
                }

            }
        } // End of Patching
    }
}
