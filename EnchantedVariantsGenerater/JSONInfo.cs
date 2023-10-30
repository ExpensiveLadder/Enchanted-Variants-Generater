using DynamicData;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using System;
using System.Collections.Generic;
using System.Text;

namespace EnchantedVariantsGenerater
{
    public class InputJSON
    {
        public string Master { get; set; } = "Skyrim.esm";
        public EnchantmentJSON[]? Enchantments { get; set; }
        public GroupJSON[]? Groups { get; set; }
    }

    public class GroupJSON
    {
        public string? GroupName { get; set; }
        public LeveledListJSON[]? LeveledLists { get; set; }
        public ItemJSON[]? Armors { get; set; }
        public ItemJSON[]? Weapons { get; set; }
        public string[]? RemoveArmors { get; set; }
        public string[]? RemoveWeapons { get; set; }
    }

    public class LeveledListJSON
    {
        public string? LeveledListPrefix { get; set; }
        public string? LeveledListSuffix { get; set; }
        public string[]? Enchantments { get; set; }
        public string[]? RemoveEnchantments { get; set; }
    }

    public class ItemJSON
    {
        public string? FormKey { get; set; }
        public string? EditorID { get; set; }
        public uint? Value { get; set; }
        public bool SetScripts { get; set; } = true;
    }

    public class EnchantmentJSON
    {
        public string? FormKey { get; set; }
        public string? EditorID { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public int? EnchantmentAmount { get; set; }
    }

    public class EnchantmentInfo
    {
        public EnchantmentInfo(EnchantmentJSON enchantment)
        {
            Enchantment = FormKey.Factory(enchantment.FormKey).ToLink<IEffectRecordGetter>();
            EnchantmentAmount = enchantment.EnchantmentAmount;
            Prefix = enchantment.Prefix;
            Suffix = enchantment.Suffix;
        }
        public IFormLink<IEffectRecordGetter> Enchantment { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public int? EnchantmentAmount { get; set; }
    }

    public class ItemInfo
    {
        public ItemInfo(ItemJSON item)
        {
            Item = FormKey.Factory(item.FormKey).ToLink<IEffectRecordGetter>();
            Value = item.Value;
            SetScripts = item.SetScripts;
        }
        public IFormLink<IEffectRecordGetter> Item { get; set; }
        public uint? Value { get; set; }
        public bool SetScripts { get; set; } = true;
    }

    public class LeveledListInfo
    {
        public LeveledListInfo(LeveledListJSON leveledList)
        {
            LeveledListPrefix = leveledList.LeveledListPrefix;
            LeveledListSuffix = leveledList.LeveledListSuffix;
            if (leveledList.Enchantments != null)
            {
                Enchantments.Add(leveledList.Enchantments);
            }
        }
        public string? LeveledListPrefix { get; set; }
        public string? LeveledListSuffix { get; set; }
        public List<string> Enchantments { get; set; } = new();
    }

    public class GroupInfo
    {
        public GroupInfo(GroupJSON group)
        {
            if (group.Weapons != null)
            {
                foreach (var weapon in group.Weapons)
                {
                    if (weapon.EditorID == null)
                    {
                        Program.DoError("Weapon has null editorID");
                        continue;
                    }
                    Weapons.Add(weapon.EditorID, new ItemInfo(weapon));
                }
            }
            if (group.Armors != null)
            {
                foreach (var armor in group.Armors)
                {
                    if (armor.EditorID == null)
                    {
                        Program.DoError("Armor has null editorID");
                        continue;
                    }
                    Weapons.Add(armor.EditorID, new ItemInfo(armor));
                }
            }
            if (group.LeveledLists != null)
            {
                foreach (var leveledlist in group.LeveledLists)
                {
                    if (leveledlist.LeveledListPrefix == null && leveledlist.LeveledListSuffix == null)
                    {
                        Program.DoError("Leveledlist has null editorID");
                        continue;
                    }
                    LeveledLists.Add(leveledlist.LeveledListPrefix + leveledlist.LeveledListSuffix, new LeveledListInfo(leveledlist));
                }
            }
        }
        public Dictionary<string, LeveledListInfo> LeveledLists { get; set; } = new();
        public Dictionary<string, ItemInfo> Armors { get; set; } = new();
        public Dictionary<string, ItemInfo> Weapons { get; set; } = new();
    }
}
