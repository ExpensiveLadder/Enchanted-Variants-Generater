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
                    enchantmentInfos.Add(ParseEnchantments(state, enchantmentGetter));
                }

                // Generate Enchanted Weapons
                if (inputWeapons.WeaponEditorIDs != null) foreach(var weaponEditorID in inputWeapons.WeaponEditorIDs)
                {
                    IWeaponGetter weaponGetter = state.LinkCache.Resolve<IWeaponGetter>(weaponEditorID); // Get base weapon
                    

                    LeveledItem LItemEnchWeapon = CreateLItemEnch(state, inputWeapons.LeveledListPrefix + weaponEditorID + inputWeapons.LeveledListSuffix);

                    foreach (var enchantmentInfo in enchantmentInfos)
                    {
                        string enchanted_weapon_EditorID = "Ench_" + weaponGetter.EditorID + "_" + enchantmentInfo.EditorID?.Replace("Ench", "");

                        if (state.LinkCache.TryResolve<IWeaponGetter>(enchanted_weapon_EditorID, out var Origenchanted_weapon))
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
                            Weapon enchanted_weapon = state.PatchMod.Weapons.DuplicateInAsNewRecord(weaponGetter); // Create new weapon and add it to patch
                            enchanted_weapon.EditorID = enchanted_weapon_EditorID;
                            enchanted_weapon.ObjectEffect.SetTo(enchantmentInfo.Enchantment); // Set enchantment to weapon
                            enchanted_weapon.Template.SetTo(weaponGetter); // Set template to base weapon
                            enchanted_weapon.Name = enchantmentInfo.Prefix + enchanted_weapon.Name + enchantmentInfo.Suffix;

                            LItemEnchWeapon.Entries?.Add(CreateLeveledItemEntry(1, enchanted_weapon.FormKey)); // Add to Leveled List
                        }
                    }

                    state.PatchMod.LeveledItems.Set(LItemEnchWeapon);
                }

            }

            /*

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

                    LeveledItem LItemEnchArmorSublists = CreateLItemEnchWeapon(state, armorGetter, "Sublists");

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

                        // Sublists
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

                            };
                            if (sublist.Entries == null) sublist.Entries = new Noggog.ExtendedList<LeveledItemEntry>();
                            sublist.Entries.Add(CreateLeveledItemEntry(enchantmentInfo.Level, armor.FormKey));

                            state.PatchMod.LeveledItems.Set(sublist);
                        }
                    }
                }

            }

            */

        } // End of Patching
    }
}
