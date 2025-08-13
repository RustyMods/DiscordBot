using System.Collections.Generic;

namespace DiscordBot;

public static class Links
{
    public static readonly Dictionary<string, string> CreatureLinks = new()
    {
        ["Boar"] = "https://static.wikia.nocookie.net/valheim/images/a/a2/Boar_trophy.png/revision/latest?cb=20210208222854",
        ["Neck"] = "https://static.wikia.nocookie.net/valheim/images/e/e7/Neck_trophy.png/revision/latest?cb=20210209235623",
        ["Greyling"] = "https://static.wikia.nocookie.net/valheim/images/4/4e/Greyling_0S.png/revision/latest?cb=20240119090219",
        ["Skeleton"] = "https://static.wikia.nocookie.net/valheim/images/d/dd/Skeleton_trophy.png/revision/latest?cb=20210215132106"
    };

    public const string DefaultAvatar = "https://gcdn.thunderstore.io/live/repository/icons/MasterTeam-Master_and_Friends-1.0.9.png.128x128_q95.png";
    public const string ServerIcon = "https://png.pngtree.com/element_our/20200702/ourmid/pngtree-web-server-vector-icon-image_2289946.jpg";
}