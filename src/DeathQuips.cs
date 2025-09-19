using System;
using System.Collections.Generic;

namespace DiscordBot;

public static class DeathQuips
{
    private static readonly Random random = new Random();
    
    private static readonly string[] templates = {
        "{player} thought they could take on {creature}{level}. They were wrong. So very wrong.",
        "Breaking news: {creature}{level} just turned {player} into yesterday's lunch!",
        "{player} has been graciously donated to the {creature}{level} retirement fund.",
        "RIP {player} - defeated by a {creature}{level}. We'll tell your story... if we remember it.",
        "{player} tried to negotiate with {creature}{level}. Negotiations failed spectacularly.",
        "{creature}{level}  just taught {player} a valuable lesson about mortality.",
        "{player} is now experiencing the afterlife, courtesy of {creature}{level}.",
        "Darwin Award goes to {player} for challenging a {creature}{level} to single combat!",
        "{creature} {level} has added {player} to their collection. How thoughtful!",
        "{player} became {creature}'s{level} afternoon snack. Crunchy on the outside, chewy on the inside.",
        "Weather update: It's raining {player}, thanks to {creature}{level}!",
        "{player} just discovered what a {creature}{level} tastes like. Spoiler: They taste like {player}.",
        "{creature} {level} sends their regards to {player}'s next of kin.",
        "{player} has been permanently relocated by {creature}{level}. New address: The Great Beyond.",
        "Medical examiner's report: {player} suffered from acute {creature}{level} syndrome."
    };
    
    private static readonly string[] lowLevelInsults = {
        "Imagine dying to a {creature}{level}. We're not angry, just disappointed.",
        "{player} was bested by a baby {creature}{level}. Let that sink in.",
        "A {creature}{level} just ended {player}'s whole career. Yikes.",
        "{player} got schooled by {creature}{level}. Time to go back to training wheels!"
    };
    
    private static readonly string[] highLevelRespect = {
        "{player} faced a legendary {creature}{level} and... well, at least they tried!",
        "{creature}{level} shows no mercy, not even for {player}. Respect the boss fight!",
        "{player} challenged a god-tier {creature}{level}. Bold strategy, poor execution.",
        "That {creature}{level} just reminded everyone why they're the apex predator. Sorry {player}."
    };
    
    private static readonly string[] bossDeaths = {
        "{player} has been obliterated by {creature}{level}. Boss fight = Boss loss!",
        "{creature}{level} just demonstrated why they're called a 'boss.' {player} learned this the hard way.",
        "{player} thought they were ready for {creature}{level}. {creature} disagreed... violently."
    };

    public static string GenerateDeathQuip(string playerName, string creatureName, int creatureLevel, bool isBoss = false)
    {
        string[] selectedTemplates;
        
        if (isBoss)
        {
            selectedTemplates = bossDeaths;
        }
        else if (creatureLevel <= 5)
        {
            selectedTemplates = lowLevelInsults;
        }
        else if (creatureLevel >= 50)
        {
            selectedTemplates = highLevelRespect;
        }
        else
        {
            selectedTemplates = templates;
        }
        
        var template = selectedTemplates[random.Next(selectedTemplates.Length)];
        var levelDisplay = creatureLevel > 1 ? " " + new string('★', creatureLevel - 1) : "";
        return template
            .Replace("{player}", playerName)
            .Replace("{creature}", creatureName)
            .Replace("{level}", levelDisplay);
    }
    
    public static string GenerateContextualQuip(string playerName, string creatureName, int creatureLevel, string prefabID = "")
    {
        var baseQuip = GenerateDeathQuip(playerName, creatureName, creatureLevel);
        
        // Add extra context based on creature type
        var contextualAdditions = prefabID switch
        {
            "Blob" => " At least it was squishy!",
            "Dragon" => " Well, that escalated quickly.",
            "Skeleton" => " Bone-chilling performance!",
            "Draugr" => " Braaaaains... or lack thereof.",
            "Wolf" => " Should've brought a bigger stick.",
            "Bjorn" => " Hibernation is over, apparently.",
            "Goblin" => " Size doesn't matter, apparently.",
            _ => ""
        };
        
        return baseQuip + contextualAdditions;
    }
    
    private static readonly Dictionary<HitData.HitType, string[]> deathQuips = new()
    {
        [HitData.HitType.Fall] = new[]
        {
            "{player} discovered gravity works. Physics: 1, {player}: 0.",
            "{player} tried to fly without wings. Spoiler alert: it didn't work.",
            "{player} has been forcibly introduced to the ground. They're getting very acquainted.",
            "{player} took the express route down. No stops, no survivors.",
            "Gravity called, {player} answered. Permanently.",
            "{player} forgot the first rule of holes: stop digging... or falling.",
            "{player} just learned why birds have wings. Too late, unfortunately."
        },
        
        [HitData.HitType.Drowning] = new[]
        {
            "{player} tried to breathe water. Fish: 1, {player}: 0.",
            "{player} has become one with the ocean. How poetic. How dead.",
            "{player} discovered they're not actually part mermaid.",
            "{player} took 'sleeping with the fishes' a bit too literally.",
            "{player} forgot to bring their floaties. Critical error.",
            "{player} just proved that humans are terrible at being fish.",
            "{player} went for a swim and stayed for eternity."
        },
        
        [HitData.HitType.Burning] = new[]
        {
            "{player} was cremated without prior consent.",
            "{player} thought they were fire-proof. They were fire-food.",
            "{player} is now extra crispy. Would you like fries with that?",
            "{player} discovered that 'stop, drop, and roll' has a time limit.",
            "{player} became a human torch. Not the superhero kind.",
            "{player} just learned why fire safety exists.",
            "{player} is now well-done. Chef's kiss! 💀"
        },
        
        [HitData.HitType.Freezing] = new[]
        {
            "{player} has been turned into a {player}-sicle.",
            "{player} got the ultimate brain freeze.",
            "{player} discovered that hypothermia isn't just a suggestion.",
            "{player} is now permanently chilled. Ice to meet you!",
            "{player} became a statue. Very artistic. Very dead.",
            "{player} learned that winter clothing isn't optional.",
            "{player} is experiencing an ice age... of one."
        },
        
        [HitData.HitType.Poisoned] = new[]
        {
            "{player} failed the poison taste test. Final score: Poison wins.",
            "{player} discovered that not all berries are friends.",
            "{player} has been chemically decommissioned.",
            "{player} took a sip from the wrong cup. Choose wisely next time!",
            "{player} learned why warning labels exist the hard way.",
            "{player} became a cautionary tale about mysterious substances.",
            "{player} is now immune to everything. Because they're dead."
        },
        
        [HitData.HitType.EdgeOfWorld] = new[]
        {
            "{player} found the edge of the world. It found them back.",
            "{player} tried to go where no one has gone before. There's a reason for that.",
            "{player} discovered that maps have boundaries for a reason.",
            "{player} took 'pushing the envelope' to the extreme.",
            "{player} has left the building... and the world... and existence.",
            "{player} went beyond the point of no return. Literally.",
            "{player} boldly went where they shouldn't have gone."
        },
        
        [HitData.HitType.Impact] = new[]
        {
            "{player} had a high-velocity meeting with something solid.",
            "{player} discovered the true meaning of 'sudden stop.'",
            "{player} became a pancake. Not the breakfast kind.",
            "{player} experienced physics at its most brutal.",
            "{player} collided with reality. Reality won.",
            "{player} learned that momentum isn't always your friend.",
            "{player} just had their final impact statement."
        },
        
        [HitData.HitType.Cart] = new[]
        {
            "{player} was run over by a cart. Talk about slow and steady losing the race.",
            "{player} discovered that carts have right of way. Aggressively.",
            "{player} became a speed bump. Permanently.",
            "{player} was cart-wheeled into the afterlife.",
            "{player} learned that carts don't brake for pedestrians.",
            "{player} got rolled over by the least threatening vehicle possible.",
            "{player} was defeated by medieval transportation. How embarrassing."
        },
        
        [HitData.HitType.Tree] = new[]
        {
            "{player} hugged a tree. The tree hugged back... harder.",
            "{player} became one with nature. Very one. Very nature.",
            "{player} discovered that trees don't move. They don't have to.",
            "{player} was branched out of existence.",
            "{player} learned that bark is worse than bite.",
            "{player} got rooted. Permanently.",
            "{player} tried to become a lumberjack. The tree disagreed."
        },
        
        [HitData.HitType.Self] = new[]
        {
            "{player} was their own worst enemy. Literally.",
            "{player} achieved the ultimate self-own.",
            "{player} discovered friendly fire isn't very friendly.",
            "{player} was defeated by their greatest foe: themselves.",
            "{player} just pulled off the world's most elaborate suicide.",
            "{player} proved that sometimes you are your own problem.",
            "{player} achieved peak self-sabotage."
        },
        
        [HitData.HitType.Structural] = new[]
        {
            "{player} was structurally readjusted. Permanently.",
            "{player} discovered that buildings fight back.",
            "{player} became part of the architecture. Very integrated.",
            "{player} was demolished by demolition.",
            "{player} learned that load-bearing walls are serious business.",
            "{player} got constructed into the afterlife.",
            "{player} experienced aggressive urban planning."
        },
        
        [HitData.HitType.Turret] = new[]
        {
            "{player} was auto-targeted and auto-eliminated.",
            "{player} discovered that turrets have excellent aim.",
            "{player} became target practice. Final score: Turret wins.",
            "{player} was precision-eliminated by automated defense.",
            "{player} learned that turrets don't take breaks.",
            "{player} got schooled by a machine with no emotions.",
            "{player} was mechanically removed from existence."
        },
        
        [HitData.HitType.Boat] = new[]
        {
            "{player} was hit by a boat. On land. Somehow.",
            "{player} discovered that boats have right of way everywhere.",
            "{player} was sailed into the afterlife.",
            "{player} got boated. That's apparently a thing now.",
            "{player} learned that boats don't brake for pedestrians either.",
            "{player} was run down by the nautical express.",
            "{player} experienced aggressive maritime law."
        },
        
        [HitData.HitType.Stalagtite] = new[]
        {
            "{player} was stabbed by the ceiling. Caves are rude.",
            "{player} discovered that nature has pointy bits.",
            "{player} was cave-shanked by limestone.",
            "{player} learned to look up the hard way.",
            "{player} became a geological casualty.",
            "{player} was speared by a very patient rock.",
            "{player} got the point. Literally."
        },
        
        [HitData.HitType.Catapult] = new[]
        {
            "{player} was launched into the stratosphere. One-way ticket.",
            "{player} discovered medieval ballistics the hard way.",
            "{player} was trebucheted out of existence.",
            "{player} experienced the superior siege weapon personally.",
            "{player} got yeeted by ancient engineering.",
            "{player} was catapulted into legend. And death.",
            "{player} learned why catapults were weapons of war."
        },
        
        [HitData.HitType.Smoke] = new[]
        {
            "{player} couldn't see through the smoke screen. Permanently.",
            "{player} was smoked out of existence.",
            "{player} discovered that smoke inhalation isn't a joke.",
            "{player} got lost in the smoke and never found their way back.",
            "{player} was fog-banked into the afterlife.",
            "{player} couldn't clear the air in time.",
            "{player} became a smokehouse casualty."
        },
        
        [HitData.HitType.Water] = new[]
        {
            "{player} was hydro-pressured into submission.",
            "{player} discovered that water can be violent.",
            "{player} was liquidated. Literally.",
            "{player} learned that H2O can be H2-NO.",
            "{player} was swept away by aquatic aggression.",
            "{player} experienced water pressure personally.",
            "{player} got tide-rolled into oblivion."
        },
        
        [HitData.HitType.CinderFire] = new[]
        {
            "{player} was cinder-blocked from life.",
            "{player} discovered that cinder fire burns differently. Deadlier.",
            "{player} was ash-ified by superior flames.",
            "{player} learned about advanced pyrotechnics the hard way.",
            "{player} was upgraded from regular fire to premium fire.",
            "{player} experienced fire 2.0. It's an improvement. For the fire.",
            "{player} was incinerated by artisanal flames."
        },
        
        [HitData.HitType.AshlandsOcean] = new[]
        {
            "{player} discovered that Ashlands water isn't refreshing.",
            "{player} went for a swim in liquid doom.",
            "{player} learned that not all oceans are created equal.",
            "{player} was dissolved by the most unfriendly sea.",
            "{player} took a bath in liquid nightmares.",
            "{player} discovered the ocean of nope.",
            "{player} went swimming and became soup."
        },
        
        [HitData.HitType.Undefined] = new[]
        {
            "{player} died to... something. We're not quite sure what.",
            "{player} was eliminated by mysterious circumstances.",
            "{player} discovered an unknown way to die. Congratulations?",
            "{player} was removed from existence by undefined means.",
            "{player} achieved death through unknown methods. Innovative!",
            "{player} died in a way that defies classification.",
            "{player} was killed by the universe's debugging process."
        }
    };
    
    public static string GenerateEnvironmentalQuip(string playerName, HitData.HitType hitType)
    {
        if (!deathQuips.TryGetValue(hitType, out var quips))
        {
            return $"{playerName} died in a way that words cannot describe. How mysterious...";
        }

        var selectedQuip = quips[random.Next(quips.Length)];
        
        return selectedQuip.Replace("{player}", playerName);
    }
}