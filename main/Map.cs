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
                Region newRegion = new Region(r.Id, newMap.GetSuperRegion(r.SuperRegion.Id), r.PlayerName, r.Armies);
                newMap.Add(newRegion);
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
    }
}