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

    //todo: this is only meant for round 0 analysis atm, might need refactoring for reuse on other rounds
    class SuperRegionsExpansionTargetSorter : System.Collections.Generic.IComparer<SuperRegion>
    {
        List<Region> picks;

        public SuperRegionsExpansionTargetSorter(List<Region> _picks) {
            picks = _picks;
        }

        public int Count(SuperRegion a)
        {
            // superregion is considered safe if player1 has all the strategic starting picks (in or neighbouring) the superregion
            // this function quantifies how safe it really is

            int count = 0;

            // no need to consider expanding into areas you already own
            if (a.OwnedByPlayer() == "player1") return 0; 
            
            // get list of all starting picks of this region
            bool redflag = false;
            foreach (Region reg in picks)
            {
                foreach(Region neighbour in reg.Neighbors) {
                    if (neighbour.SuperRegion.Id == a.Id) {
                        switch (reg.PlayerName)
                        {
                            case "player1":
                                count += 2;
                                break;
                            case "player2":
                                redflag = true;
                                break;
                            case "neutral":
                                count++;
                                break;
                            case "unknown":
                                //todo: check if the pick was picked by me in lower position then my last gotten pick
                                count--;
                                //todo: instead of giving it lower priority, it could also be worth increasing the aggressivity
                                break;
                        }
                    
                        continue; //we only need to check one of the found neighbours
                    }
                }
            }

            if (redflag) count = 0;
            
            return count;
        }

        public int Compare(SuperRegion a, SuperRegion b)
        {
            return Count(a) - Count(b);
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
                b.Neighbors.Sort(new RegionsNeighbouringSuperRegionSorter(list));

                // prioritize neighbouring a good superregion
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

    class RegionsAvailableArmiesSorter : System.Collections.Generic.IComparer<Region>
    {

        public RegionsAvailableArmiesSorter()
        {
        }

        public int Compare(Region a, Region b)
        {
            int aArmies = a.Armies + a.PledgedArmies - a.ReservedArmies;
            int bArmies = b.Armies + b.PledgedArmies - b.ReservedArmies;

            return aArmies - bArmies;
        }
    }


}
