using System;
using System.Collections.Generic;
using System.Text;

namespace EnchantedVariantsGenerater
{
    public class Config
    {
        public bool CheckExistingGenerated { get; set; } = true;
    }
    public class InputJSON
    {
        public string[]? RequiredMods { get; set; }
        public ItemJSON[]? Armors { get; set; }
        public ItemJSON[]? Weapons { get; set; }
        public EnchantmentJSON[]? Enchantments { get; set; }
        public string LeveledListPrefix { get; set; } = "SublistEnch_";
        public string LeveledListSuffix { get; set; } = "";
        public int PriorityOrder = 1;
    }

    public class ItemJSON
    {
        public string[]? RequiredMods { get; set; }
        public string? FormKey { get; set; }
        public string? EditorID { get; set; }
        public uint? Value { get; set; }
    }

    public class EnchantmentJSON
    {
        public string[]? RequiredMods { get; set; }
        public string? FormKey { get; set; }
        public string? EditorID { get; set; }
        public string? Prefix { get; set; }
        public string? Suffix { get; set; }
        public int? EnchantmentAmount { get; set; }
        public string? Mode { get; set; }
    }
}
