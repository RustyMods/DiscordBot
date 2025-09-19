using System.Collections.Generic;

namespace DiscordBot;

public static class Links
{
    public static string GetCreatureIcon(string creatureID, string defaultURL = "")
    {
        string normalized = creatureID.Replace("(Clone)", string.Empty);
        return CreatureLinks.TryGetValue(normalized, out var link) ? link : defaultURL;
    }

    private static readonly Dictionary<string, string> CreatureLinks = new()
    {
        // Meadows
        ["Boar"] = "https://valheim.fandom.com/wiki/Special:FilePath/Boar_trophy.png",
        ["Deer"] = "https://valheim.fandom.com/wiki/Special:FilePath/Deer_trophy.png",
        ["Neck"] = "https://valheim.fandom.com/wiki/Special:FilePath/Neck_trophy.png",
        ["Greyling"] = "https://valheim.fandom.com/wiki/Special:FilePath/Greyling_0S.png",

        // Black Forest
        ["Greydwarf"] = "https://valheim.fandom.com/wiki/Special:FilePath/Greydwarf_trophy.png",
        ["Greydwarf_Elite"] = "https://valheim.fandom.com/wiki/Special:FilePath/Greydwarf_Brute_trophy.png",
        ["Greydwarf_Shaman"] = "https://valheim.fandom.com/wiki/Special:FilePath/Greydwarf_Shaman_trophy.png",
        ["Troll"] = "https://valheim.fandom.com/wiki/Special:FilePath/Troll_trophy.png",
        ["Skeleton"] = "https://valheim.fandom.com/wiki/Special:FilePath/Skeleton_trophy.png",
        ["Skeleton_Poison"] = "https://valheim.fandom.com/wiki/Special:FilePath/Rancid_Remains_trophy.png",
        ["Ghost"] = "https://valheim.fandom.com/wiki/Special:FilePath/Ghost_0star.png",
        ["Bjorn"] = "https://static.wikia.nocookie.net/valheim/images/a/a4/Bear.png",
        // Swamp
        ["Draugr"] = "https://valheim.fandom.com/wiki/Special:FilePath/Draugr_trophy.png",
        ["Draugr_Elite"] = "https://valheim.fandom.com/wiki/Special:FilePath/Draugr_Elite_trophy.png",
        ["Draugr_Ranged"] = "https://valheim.fandom.com/wiki/Special:FilePath/Draugr_Ranged_trophy.png",
        ["Blob"] = "https://valheim.fandom.com/wiki/Special:FilePath/Blob_trophy.png",
        ["Leech"] = "https://valheim.fandom.com/wiki/Special:FilePath/Leech_trophy.png",
        ["Abomination"] = "https://valheim.fandom.com/wiki/Special:FilePath/Abomination_trophy.png",
        ["Wraith"] = "https://valheim.fandom.com/wiki/Special:FilePath/Wraith_trophy.png",
        ["Surtling"] = "https://valheim.fandom.com/wiki/Special:FilePath/Surtling_trophy.png",

        // Mountain
        ["Wolf"] = "https://valheim.fandom.com/wiki/Special:FilePath/Wolf_trophy.png",
        ["Hatchling"] = "https://valheim.fandom.com/wiki/Special:FilePath/Drake_trophy.png",
        ["Fenring"] = "https://valheim.fandom.com/wiki/Special:FilePath/Fenring_trophy.png",
        ["Fenring_Cultist"] = "https://valheim.fandom.com/wiki/Special:FilePath/Cultist_trophy.png",
        ["Ulv"] = "https://valheim.fandom.com/wiki/Special:FilePath/Ulv_trophy.png",
        ["StoneGolem"] = "https://valheim.fandom.com/wiki/Special:FilePath/Stone_Golem_trophy.png",
        ["Bat"] = "https://valheim.fandom.com/wiki/Special:FilePath/Bat.png",

        // Plains
        ["Deathsquito"] = "https://valheim.fandom.com/wiki/Special:FilePath/Deathsquito_trophy.png",
        ["Lox"] = "https://valheim.fandom.com/wiki/Special:FilePath/Lox_trophy.png",
        ["Goblin"] = "https://valheim.fandom.com/wiki/Special:FilePath/Fuling_trophy.png",
        ["GoblinShaman"] = "https://valheim.fandom.com/wiki/Special:FilePath/Fuling_Shaman_trophy.png",
        ["GoblinBrute"] = "https://valheim.fandom.com/wiki/Special:FilePath/Fuling_Berserker_trophy.png",
        ["BlobTar"] = "https://valheim.fandom.com/wiki/Special:FilePath/Growth_trophy.png",
        ["GoblinArcher"] = "https://valheim.fandom.com/wiki/Special:FilePath/Fuling_trophy.png",
        ["Unbjorn"] = "https://static.wikia.nocookie.net/valheim/images/8/88/Vile.png",

        // Ocean
        ["Serpent"] = "https://valheim.fandom.com/wiki/Special:FilePath/Serpent_trophy.png",

        // Mistlands
        ["Seeker"] = "https://valheim.fandom.com/wiki/Special:FilePath/Seeker_trophy.png",
        ["SeekerBrute"] = "https://valheim.fandom.com/wiki/Special:FilePath/Seeker_soldier_trophy.png",
        ["Tick"] = "https://valheim.fandom.com/wiki/Special:FilePath/Tick_trophy.png",
        ["Gjall"] = "https://valheim.fandom.com/wiki/Special:FilePath/Gjall_trophy.png",
        ["Hare"] = "https://valheim.fandom.com/wiki/Special:FilePath/Hare_trophy.png",
        ["SeekerBrood"] = "https://valheim.fandom.com/wiki/Special:FilePath/Seeker_Brood.png",
        ["Dverger"] = "https://valheim.fandom.com/wiki/Special:FilePath/Dvergr_trophy.png",
        ["DvergerMage"] = "https://valheim.fandom.com/wiki/Special:FilePath/Dvergr_trophy.png",
        ["DvergerMageFire"] = "https://valheim.fandom.com/wiki/Special:FilePath/Dvergr_trophy.png",
        ["DvergerMageIce"] = "https://valheim.fandom.com/wiki/Special:FilePath/Dvergr_trophy.png",
        ["DvergerMageSupport"] = "https://valheim.fandom.com/wiki/Special:FilePath/Dvergr_trophy.png",
        // Ashlands
        ["Charred_Melee"] = "https://valheim.fandom.com/wiki/Special:FilePath/Warrior_trophy.png",
        ["Charred_Ranged"] = "https://valheim.fandom.com/wiki/Special:FilePath/Marksman_trophy.png",
        ["Asksvin"] = "https://valheim.fandom.com/wiki/Special:FilePath/Asksvin_trophy.png",
        ["Morgen"] = "https://valheim.fandom.com/wiki/Special:FilePath/Morgen_trophy.png",
        ["FallenValkyrie"] = "https://valheim.fandom.com/wiki/Special:FilePath/Fallen_Valkyrie_trophy.png",
        ["Kvastur"] = "https://valheim.fandom.com/wiki/Special:FilePath/Kvastur_trophy.png",
        ["Volture"] = "https://valheim.fandom.com/wiki/Special:FilePath/Volture_trophy.png",
        ["Charred_Mage"] = "https://valheim.fandom.com/wiki/Special:FilePath/Warlock_trophy.png",
        ["Charred_Melee_Dyrnwyn"] = "https://valheim.fandom.com/wiki/Special:FilePath/Lord_Reto_0S.png",
        ["BonemawSerpent"] = "https://valheim.fandom.com/wiki/Special:FilePath/BonemawSerpent_trophy.png",
        ["DvergerAshlands"] = "https://valheim.fandom.com/wiki/Special:FilePath/Dvergr_trophy.png",
        ["Skugg"] = "https://valheim.fandom.com/wiki/Special:FilePath/Skugg_trophy.png",

        // Hildir minibosses
        ["Skeleton_Hildir"] = "https://valheim.fandom.com/wiki/Special:FilePath/Skeleton_Hildir_trophy.png",
        ["Fenring_Cultist_Hildir"] = "https://valheim.fandom.com/wiki/Special:FilePath/Fenring_Cultist_Hildir_trophy.png",
        ["GoblinShaman_Hildir"] = "https://valheim.fandom.com/wiki/Special:FilePath/Zil_trophy.png",
        ["GoblinBrute_Hildir"] = "https://valheim.fandom.com/wiki/Special:FilePath/Thungr_trophy.png",
        
        ["Eikthyr"] = "https://valheim.fandom.com/wiki/Special:FilePath/Eikthyr_trophy.png",
        ["gd_king"] = "https://valheim.fandom.com/wiki/Special:FilePath/The_Elder_trophy.png",
        ["Bonemass"] = "https://valheim.fandom.com/wiki/Special:FilePath/Bonemass_trophy.png",
        ["Dragon"] = "https://valheim.fandom.com/wiki/Special:FilePath/Moder_trophy.png",
        ["GoblinKing"] = "https://valheim.fandom.com/wiki/Special:FilePath/Yagluth_trophy.png",
        ["SeekerQueen"] = "https://valheim.fandom.com/wiki/Special:FilePath/The_Queen_trophy.png",
        ["Fader"] = "https://valheim.fandom.com/wiki/Special:FilePath/Fader_trophy.png",
    };


    public const string DefaultAvatar = "https://gcdn.thunderstore.io/live/repository/icons/MasterTeam-Master_and_Friends-1.0.9.png.128x128_q95.png";
    public const string ServerIcon = "https://png.pngtree.com/element_our/20200702/ourmid/pngtree-web-server-vector-icon-image_2289946.jpg";
}