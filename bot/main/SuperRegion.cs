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
        public int tempSortValue;


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
       /* public string OwnedByPlayer()
    	{
    		String playerName = subRegions.First().PlayerName;
    		foreach(Region region in subRegions)
    		{
    			if (playerName != region.PlayerName)
    				return null;
    		}
    		return playerName;
    	}
        
        useless function if our subregions are not updated every turn
        */

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

        public bool IsFinishable(int income, string myName){

            int nn = 0;
            int nc = 0;
            int p1n = 0;
            int p1c = income;

            foreach (Region reg in SubRegions)
            {
                switch(reg.PlayerName) {
                    case "neutral":
                        nn++;
                        nc += reg.Armies;
                        break;
                    case "player1":
                        if (myName == "player1")
                        {
                            p1n++;
                            p1c += reg.Armies - 1; //-1 because you cant attack with your last army, it needs to stay to secure region
                        }
                        else return false;
                       break;
                    case "player2":
                        if (myName == "player2")
                        {
                            p1n++;
                            p1c += reg.Armies - 1; //-1 because you cant attack with your last army, it needs to stay to secure region
                        }
                        else return false;
                        break;
                    case "unknown":
                        return false;
                        //break;
                }
            }

            // if player1 + income has double number of armies of neutrals, we can finish this territory
            if (p1n > nn * 2) return true;

            //todo: might be worth to refactor this to return or store an array of deployment and move instruction
            //todo: might be worth to refactor this later to create strategy to attack player2

            return true;
        }
    }

}