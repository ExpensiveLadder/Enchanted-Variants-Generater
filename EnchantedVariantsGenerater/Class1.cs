using System;
using System.Collections.Generic;
using System.Text;

namespace EnchantedVariantsGenerater
{
    /*
    public class Config
    {
        public bool CheckExistingGenerated { get; set; } = true;
        public bool VerboseLogging { get; set; } = false;
    }
    */

    public class InputJSON
    {
        public string Master { get; set; } = "Skyrim.esm";
        public ItemJSON[]? Armors { get; set; }
        public ItemJSON[]? Weapons { get; set; }
        public EnchantmentJSON[]? Enchantments { get; set; }
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

    /*

    public class ItemInfo
    {
        public ItemInfo(string formKey, string editorID, uint value, bool setScripts)
        {
            FormKey = formKey;
            EditorID = editorID;
            Value = value;
            SetScripts = setScripts;
        }
        public string FormKey { get; }
        public string EditorID { get; }
        public uint? Value { get; }
        public bool SetScripts { get; } = true;
    }

    public class EnchantmentInfo
    {
        public string FormKey { get; set; }
        public string Prefix { get; set; }
        public string Suffix { get; set; }
        public int EnchantmentAmount { get; set; }
    }
    */
}
