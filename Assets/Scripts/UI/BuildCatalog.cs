using System.Collections.Generic;

namespace BuildATower
{
    public sealed class BuildCatalogFamily
    {
        public BuildFamily Family;
        public string Label;
        public List<BuildCatalogSubgroup> Subgroups = new();
        public List<RoomTypeSO> Rooms = new();
    }

    public sealed class BuildCatalogSubgroup
    {
        public BuildSubgroup Subgroup;
        public string Label;
        public List<RoomTypeSO> Rooms = new();
    }

    /// <summary>
    /// Groups placeable room types into nested Build HUD families.
    /// Empty Utility (and empty shop subgroups) are omitted until assets exist.
    /// </summary>
    public static class BuildCatalog
    {
        static readonly BuildFamily[] FamilyOrder =
        {
            BuildFamily.Office,
            BuildFamily.Hotel,
            BuildFamily.Condo,
            BuildFamily.Shops,
            BuildFamily.Utility,
            BuildFamily.Transit
        };

        public static List<BuildCatalogFamily> Group(IEnumerable<RoomTypeSO> rooms)
        {
            var byFamily = new Dictionary<BuildFamily, List<RoomTypeSO>>();
            foreach (var room in rooms)
            {
                if (room == null || room.isLobby) continue;
                var family = room.ResolvedBuildFamily();
                if (family == BuildFamily.None) continue;
                if (!byFamily.TryGetValue(family, out var list))
                {
                    list = new List<RoomTypeSO>();
                    byFamily[family] = list;
                }

                if (!list.Contains(room))
                    list.Add(room);
            }

            var result = new List<BuildCatalogFamily>();
            foreach (var family in FamilyOrder)
            {
                if (!byFamily.TryGetValue(family, out var list) || list.Count == 0)
                    continue;

                var entry = new BuildCatalogFamily
                {
                    Family = family,
                    Label = FamilyLabel(family)
                };

                if (family == BuildFamily.Shops)
                {
                    AddShopSubgroup(entry, BuildSubgroup.Food, list);
                    AddShopSubgroup(entry, BuildSubgroup.Retail, list);
                    if (entry.Subgroups.Count == 0)
                        continue;
                }
                else
                {
                    entry.Rooms.AddRange(list);
                }

                result.Add(entry);
            }

            return result;
        }

        static void AddShopSubgroup(BuildCatalogFamily entry, BuildSubgroup subgroup, List<RoomTypeSO> shops)
        {
            var rooms = new List<RoomTypeSO>();
            foreach (var room in shops)
            {
                if (room.ResolvedBuildSubgroup() == subgroup)
                    rooms.Add(room);
            }

            if (rooms.Count == 0) return;
            entry.Subgroups.Add(new BuildCatalogSubgroup
            {
                Subgroup = subgroup,
                Label = SubgroupLabel(subgroup),
                Rooms = rooms
            });
        }

        public static string FamilyLabel(BuildFamily family) => family switch
        {
            BuildFamily.Office => "Office",
            BuildFamily.Hotel => "Hotel",
            BuildFamily.Condo => "Condo",
            BuildFamily.Shops => "Shops",
            BuildFamily.Utility => "Utility",
            BuildFamily.Transit => "Transit",
            _ => "Other"
        };

        public static string SubgroupLabel(BuildSubgroup subgroup) => subgroup switch
        {
            BuildSubgroup.Food => "Food",
            BuildSubgroup.Retail => "Retail",
            _ => "Other"
        };
    }
}
