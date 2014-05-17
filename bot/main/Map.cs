using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace main
{


    public class Map
    {

        public List<Region> regions;
        public List<SuperRegion> superRegions;

        public Map()
        {
            this.regions = new List<Region>();
            this.superRegions = new List<SuperRegion>();
        }

        public Map(List<Region> regions, List<SuperRegion> superRegions)
        {
            this.regions = regions;
            this.superRegions = superRegions;
        }

        /**
         * Add a Region to the map
         * @param region : Region to be Added
         */
        public void Add(Region region)
        {
            foreach (Region r in regions)
                if (r.Id == region.Id)
                {
                    Console.Error.WriteLine("Region cannot be Added: id already exists.");
                    return;
                }
            regions.Add(region);
        }

        /**
         * Add a SuperRegion to the map
         * @param superRegion : SuperRegion to be Added
         */
        public void Add(SuperRegion superRegion)
        {
            foreach (SuperRegion sr in superRegions)
                if (sr.Id == superRegion.Id)
                {
                    Console.Error.WriteLine("SuperRegion cannot be Added: id already exists.");
                    return;
                }
            superRegions.Add(superRegion);
        }

        /**
         * @return : a new Map object exactly the same as this one
         */
        public Map GetMapCopy()
        {
            Map newMap = new Map();
            foreach (SuperRegion sr in superRegions) //copy superRegions
            {
                SuperRegion newSuperRegion = new SuperRegion(sr.Id, sr.ArmiesReward);
                newMap.Add(newSuperRegion);
            }
            foreach (Region r in regions) //copy regions
            {
                try
                {
                    Region newRegion = new Region(r.Id, newMap.GetSuperRegion(r.SuperRegion.Id), r.PlayerName, r.Armies);
                    newMap.Add(newRegion);
                }
                catch (Exception exc) {
                    Console.Error.WriteLine(exc.Message);
                    Console.Error.WriteLine("couldn't copy region");
                    Console.Error.WriteLine("id: " + r.Id);
                    Console.Error.WriteLine("parent: " + r.SuperRegion.Id);
                    Console.Error.WriteLine("playername: " + r.PlayerName);
                    Console.Error.WriteLine("armies: " + r.Armies);
                }
            }
            foreach (Region r in regions) //Add neighbors to copied regions
            {
                Region newRegion = newMap.GetRegion(r.Id);
                foreach (Region neighbor in r.Neighbors)
                    newRegion.AddNeighbor(newMap.GetRegion(neighbor.Id));
            }
            return newMap;
        }

        /**
         * @param id : a Region id number
         * @return : the matching Region object
         */
        public Region GetRegion(int id)
        {
            foreach (Region region in regions)
                if (region.Id == id)
                    return region;

            return null;
        }

        /**
         * @param id : a SuperRegion id number
         * @return : the matching SuperRegion object
         */
        public SuperRegion GetSuperRegion(int id)
        {
            foreach (SuperRegion superRegion in superRegions)
                if (superRegion.Id == id)
                    return superRegion;

            Console.Error.WriteLine("couldn't find SuperRegion with id " + id);
            return null;
        }

        public List<Region> Regions
        {
            get { return regions; }
        }

        public List<SuperRegion> SuperRegions
        {
            get { return superRegions; }
        }

        public String MapString
        {
            get { return string.Join(" ", regions.Select(region => region.Id + ";" + region.PlayerName + ";" + region.Armies)); }
        }

        public bool AllSuperRegionsInSight()
        {
            bool all = true;
            foreach (SuperRegion sr in superRegions)
            {
                if (!sr.InSight) all = false;
            }
            return all;
        }

        public bool RegionBelongsToEnemySuperRegion(int id, String myName)
        {
            Region thisregion = GetRegion(id);
            SuperRegion thissr = GetSuperRegion(thisregion.SuperRegion.Id);

            foreach (Region reg in thissr.SubRegions)
            {
                Region rn = GetRegion(reg.Id);
                if (rn.OwnedByPlayer(myName)) return false;
                if (rn.OwnedByPlayer("neutral")) return false;
            }

            return true;
        }

        public bool RegionBelongsToOurSuperRegion(int id, String myName)
        {
            Region thisregion = GetRegion(id);
            SuperRegion thissr = GetSuperRegion(thisregion.SuperRegion.Id);

            foreach (Region reg in thissr.SubRegions)
            {
                Region rn = GetRegion(reg.Id);
                if (!rn.OwnedByPlayer(myName)) return false;
            }

            return true;
        }

        public bool RegionBordersOneOfOurOwnSuperRegions(int id)
        {
            Region thisregion = GetRegion(id);

            foreach (Region ne in thisregion.Neighbors)
            {
                SuperRegion sr = GetSuperRegion(ne.SuperRegion.Id);
                if (sr.ownedByUs) return true;
            }

            return false;
        }

        public int FindNextStep(int dest)
        {
            // reset
            foreach (Region reg in regions)
            {
                reg.tempSortValue = 0;
            }

            // initial list
            List<Region> list = new List<Region>();
            foreach (Region reg in GetRegion(dest).Neighbors)
            {
                list.Add(reg);
            }

            // first iteration
            int it = 1;

            while (list.Count > 0)
            {
                List<Region> newlist = new List<Region>();
                foreach (Region testRegion in list)
                {
                    Region reg = GetRegion(testRegion.Id);
                    if (reg.OwnedByPlayer("neutral"))
                    {
                        return testRegion.Id;
                    }
                    else
                    {
                        // havent found home yet, prepare next iteration
                        if (reg.tempSortValue == 0)
                        {
                            reg.tempSortValue = it;
                            foreach (Region neigh in reg.Neighbors)
                            {
                                if (!newlist.Contains(neigh) && (neigh.tempSortValue == 0)) newlist.Add(neigh);
                            }
                        }
                    }
                }

                list.Clear();
                foreach (Region nl in newlist)
                {
                    list.Add(nl);
                }

                it++;
            }

            return -1; // failed to find home
        }

        public bool SuperRegionBelongsTo(int id, String thisName)
        {
            SuperRegion thissr = GetSuperRegion(id);

            foreach (Region reg in thissr.SubRegions)
            {
                Region rn = GetRegion(reg.Id);
                if (!rn.OwnedByPlayer(thisName)) return false;
            }

            return true;
        }

        public bool SuperRegionCouldBelongToOpponent(int id, String opponentName)
        {
            SuperRegion thissr = GetSuperRegion(id);

            foreach (Region reg in thissr.SubRegions)
            {
                Region rn = GetRegion(reg.Id);
                if (!rn.OwnedByPlayer(opponentName) && !rn.OwnedByPlayer("unknown")) return false;
            }

            return true;
        }

    }
}