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

    class RegionsSorter : System.Collections.Generic.IComparer<Region>
    {
        private List<SuperRegion> list;

        public RegionsSorter(List<SuperRegion> _list) {
            list = _list;
        }

        //todo: if a region is neighbouring a good superregion, push it up

        public int Compare(Region a, Region b)
        {
            return list.IndexOf(a.SuperRegion) - list.IndexOf(b.SuperRegion);
        }
    }

}
