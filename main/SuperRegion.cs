using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace main
{

    public class SuperRegion
    {

        private int id;
        private int armiesReward;
        private List<Region> subRegions;

        public SuperRegion(int id, int armiesReward)
        {
            this.id = id;
            this.armiesReward = armiesReward;
            subRegions = new List<Region>();
        }

        public void AddSubRegion(Region subRegion)
        {
            if (!subRegions.Contains(subRegion))
                subRegions.Add(subRegion);
        }

        /**
         * @return A string with the name of the player that fully owns this SuperRegion
         */
        public string OwnedByPlayer()
    	{
    		String playerName = subRegions.First().PlayerName;
    		foreach(Region region in subRegions)
    		{
    			if (playerName != region.PlayerName)
    				return null;
    		}
    		return playerName;
    	}

        public int Id
        {
            get { return id; }
        }

        public int ArmiesReward
        {
            get { return armiesReward; }
        }

        public List<Region> SubRegions
        {
            get { return subRegions; }
        }

    }

}