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

        public int numberOfRegionsOwnedByUs;
        public int numberOfRegionsOwnedByOpponent;
        public bool ownedByUs;
        public bool inSight;

        public SuperRegion(int id, int armiesReward)
        {
            this.id = id;
            this.armiesReward = armiesReward;
            this.subRegions = new List<Region>();

            this.tempSortValue = 0;
            this.numberOfRegionsOwnedByUs = -1;
            this.numberOfRegionsOwnedByOpponent = -1;
            this.ownedByUs = false;
            this.inSight = false;
        }

        public void AddSubRegion(Region subRegion)
        {
            if (!subRegions.Contains(subRegion))
                subRegions.Add(subRegion);
        }

        /**
         * @return A string with the name of the player that fully owns this SuperRegion
         */
        public string OwnedByPlayer(Map map)
    	{
    		String playerName = map.GetRegion(subRegions.First().Id).PlayerName;
    		foreach(Region region in subRegions)
    		{
                Region mapr = map.GetRegion(region.Id);
    			if (playerName != mapr.PlayerName)
    				return null;
    		}
    		return playerName;
    	}

        public bool MightBeOwnedBy(Map map, String enemyName)
        {
            bool owned = true;
            foreach (Region region in subRegions)
            {
                Region mapr = map.GetRegion(region.Id);
                String playerName = mapr.PlayerName;
                if ((playerName != enemyName) && (playerName != "unknown")) owned = false;
            }
            return owned;
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
            if (p1c >= nn * 4 - 1) return true;
            // -1 because we can get lucky with a 3vs2 on FTB but we usually want all attacks to be 4vs2, no more then 1 3vs2 per attempt to finish superregion

            //todo: might be worth to refactor this to return or store an array of deployment and move instruction
            //todo: might be worth to refactor this later to create strategy to attack player2

            return false;
        }


        public bool IsSafeToFinish(bot.BotState state)
        {
            bool finishableSuperRegion = true;
            string opponentName = state.OpponentPlayerName;
            string myName = state.MyPlayerName;

            // if superregion has enemy or is already bordered by enemy, then its not very safe
            // might aswell not consider it finishable and don't waste armies on it
            // unless it is only bordering in a single territory and we have stack advantage/equality on it
            foreach (Region reg in SubRegions)
            {
                Region rn = state.FullMap.GetRegion(reg.Id);
                if (rn.OwnedByPlayer(opponentName))
                {
                    finishableSuperRegion = false;
                    break;
                }
                foreach (Region neigh in rn.Neighbors)
                {
                    Region ni = state.FullMap.GetRegion(neigh.Id);
                    if (ni.OwnedByPlayer(opponentName))
                    {
                        // enemy is bordering, beware

                        // check with how many armies and how many areas is the enemy bordering us
                        int nborders = 0;
                        int enarmies = 0;
                        int ourarmies = 0;
                        foreach (Region ourregions in ni.Neighbors)
                        {
                            Region our = state.FullMap.GetRegion(ourregions.Id);
                            if (our.OwnedByPlayer(myName))
                            {
                                nborders++;
                                ourarmies = our.Armies;
                                enarmies = ni.Armies;
                            }
                        }

                        // if it's a 1 on 1 border and we have stack equality give or take a couple armies
                        // or stacks are higher then 20
                        // let it proceed (superregion is still finishable)
                        if (((nborders == 1) && (ourarmies + 2 >= enarmies)) || ((nborders == 1) && (ourarmies > 20))) continue;

                        // else, it's a bad idea to finish this region
                        finishableSuperRegion = false;
                        break;
                    }
                }
                if (!finishableSuperRegion) break;
            }

            return finishableSuperRegion;
        }

        public bool InSight
        {
            set { inSight = value; }
            get { return inSight; }
        }

        public void checkInSight(Map map)
        {
            inSight = false;

            foreach (Region reg in SubRegions)
            {
                if (!map.GetRegion(reg.Id).OwnedByPlayer("unknown")) inSight = true;
            }
        }
    }

}