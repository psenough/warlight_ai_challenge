using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace main
{

    class SuperRegionsLowerArmiesSorter : System.Collections.Generic.IComparer<SuperRegion>
    {
        public SuperRegionsLowerArmiesSorter() { }

        public int Compare(SuperRegion a, SuperRegion b)
        {
            return a.ArmiesReward - b.ArmiesReward;
        }
    }



    class RegionsImportanceSorter : System.Collections.Generic.IComparer<Region>
    {
        private List<SuperRegion> list;

        public RegionsImportanceSorter(List<SuperRegion> _list) {
            list = _list;
        }

        public int Compare(Region a, Region b)
        {
            a.Neighbors.Sort(new RegionsNeighbouringSuperRegionSorter(list));
          
            // if neighbouring a higher ranked superregion
            if (a.SuperRegion.Id != a.Neighbors[0].Id)
            {
                // prioritize neighbouring a good superregion
                b.Neighbors.Sort(new RegionsNeighbouringSuperRegionSorter(list));
                return a.Neighbors[0].SuperRegion.ArmiesReward - b.Neighbors[0].SuperRegion.ArmiesReward;
            } else {
                // prioritize regions within higher ranked superregion
                return list.IndexOf(a.SuperRegion) - list.IndexOf(b.SuperRegion);
            }
        }
    }

    class RegionsNeighbouringSuperRegionSorter : System.Collections.Generic.IComparer<Region>
    {
        private List<SuperRegion> list;

        public RegionsNeighbouringSuperRegionSorter(List<SuperRegion> _list)
        {
            list = _list;
        }

        public int Compare(Region a, Region b)
        {
            return list.IndexOf(a.SuperRegion) - list.IndexOf(b.SuperRegion);
        }
    }


}
