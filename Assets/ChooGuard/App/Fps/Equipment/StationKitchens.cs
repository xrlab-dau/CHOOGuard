using System;
using System.Collections.Generic;

namespace ChooGuard.App.Fps.Equipment
{
    /// <summary>
    /// What each food shop of the twin cooks with, by a word of its name. The twin models the shops as empty rooms; which
    /// appliances stand in them is an assumption made once here (there is no public source for a station tenant's fuel or
    /// line-up): shops that simmer, steam or fry in a pan on a burner use city gas (LNG), shops that deep-fry or bake use
    /// electric appliances. The editor builder places the kitchens from this table, and IncidentDirector.KitchenOf
    /// (the rest of the game asks whether a shop has a kitchen) reads the same rows.
    /// </summary>
    public static class StationKitchens
    {
        /// <summary>One shop's cooking line, in the order the appliances stand along the side wall.</summary>
        public sealed class Spec
        {
            /// <summary>A word of the shop's name (the twin's shop label).</summary>
            public readonly string Word;
            /// <summary>The primary appliance, in English (JEV's descriptions) and Korean, and whether it fries in oil.</summary>
            public readonly string English, Korean;
            public readonly bool Oil;
            /// <summary>Stations along the wall: "fryer", "range:&lt;cookware&gt;" (city-gas burner with a pan or pot on it), "oven", "table".</summary>
            public readonly string[] Line;

            public Spec(string word, string english, string korean, bool oil, params string[] line)
            {
                Word = word;
                English = english;
                Korean = korean;
                Oil = oil;
                Line = line;
            }

            /// <summary>The shop burns city gas: it has a range, a meter and the valves, hoses and alarm that go with them.</summary>
            public bool Gas
            {
                get
                {
                    foreach (var station in Line) if (station.StartsWith("range", StringComparison.Ordinal)) return true;
                    return false;
                }
            }
        }

        public static readonly IReadOnlyList<Spec> All = new[]
        {
            new Spec("닭강정", "deep fryer", "튀김기", true, "fryer", "fryer", "table"),
            new Spec("어묵", "fish-cake fryer", "튀김기", true, "fryer", "range:StockPot", "table"),
            new Spec("도넛", "doughnut fryer", "튀김기", true, "fryer", "fryer", "table"),
            new Spec("도나스", "doughnut fryer", "튀김기", true, "fryer", "table"),
            new Spec("떡볶이", "gas stove", "가스레인지", false, "range:FryPan", "table"),
            new Spec("김밥", "gas stove", "가스레인지", false, "range:FryPan", "table"),
            new Spec("한식", "gas stove", "가스레인지", false, "range:Wok", "range:StockPot", "table"),
            new Spec("명가", "gas stove", "가스레인지", false, "range:StockPot", "table"),
            new Spec("SUBWAY", "toaster oven", "오븐", false, "oven", "table"),
            new Spec("제과", "bakery oven", "오븐", false, "oven", "oven", "table"),
            new Spec("단팥빵", "bakery oven", "오븐", false, "oven", "table"),
            new Spec("떡공방", "rice-cake steamer", "찜기", false, "range:SteamerPot", "table"),
        };

        /// <summary>The kitchen of the shop with this label, or null when it has none (a cafe, a pharmacy, a convenience store).</summary>
        public static Spec Of(string shopLabel)
        {
            foreach (var spec in All) if (shopLabel.Contains(spec.Word)) return spec;
            return null;
        }
    }
}
