using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace main
{



    public class Region
    {

        private int id;
        private List<Region> neighbors;
        private SuperRegion superRegion;
        private int armies;
        private int previousturnarmies;
        private bool deployedthisturn;
        //private bool deployedlastturn;
        private String playerName;

        // temp variables
        private int reservedArmies; // reserved for an attack
        private int pledgedArmies; // pledged being added at this turn

        public int tempSortValue;

        public Region(int id, SuperRegion superRegion)
        {
            this.id = id;
            this.superRegion = superRegion;
            this.neighbors = new List<Region>();
            this.playerName = "unknown";
            this.armies = 0;
            this.previousturnarmies = 0;
            this.deployedthisturn = false;
            //this.deployedlastturn = false;

            this.reservedArmies = 0;
            this.pledgedArmies = 0;

            superRegion.AddSubRegion(this);
        }

        public Region(int id, SuperRegion superRegion, String playerName, int armies)
        {
            this.id = id;
            this.superRegion = superRegion;
            this.neighbors = new List<Region>();
            this.playerName = playerName;
            this.armies = armies;
            this.previousturnarmies = armies;
            this.deployedthisturn = false;
            //this.deployedlastturn = false;

            this.reservedArmies = 0;
            this.pledgedArmies = 0;
            
            superRegion.AddSubRegion(this);
        }

        public void AddNeighbor(Region neighbor)
        {
            if (!neighbors.Contains(neighbor))
            {
                neighbors.Add(neighbor);
                neighbor.AddNeighbor(this);
            }
        }

        /**
         * @param region a Region object
         * @return True if this Region is a neighbor of given Region, false otherwise
         */
        public bool IsNeighbor(Region region)
        {
            if (neighbors.Contains(region))
                return true;
            return false;
        }

        /**
         * @param playerName A string with a player's name
         * @return True if this region is owned by given playerName, false otherwise
         */
        public bool OwnedByPlayer(String playerName)
        {
            if (playerName == this.playerName)
                return true;
            return false;
        }

        public int Armies
        {
            set { armies = value; }
            get { return armies; }
        }

        public int PreviousTurnArmies
        {
            set { previousturnarmies = value; }
            get { return previousturnarmies; }
        }

        public String PlayerName
        {
            set { playerName = value; }
            get { return playerName; }
        }

        public int Id
        {
            get { return id; }
        }

        public List<Region> Neighbors
        {
            get { return neighbors; }
        }

        public SuperRegion SuperRegion
        {
            get { return superRegion; }
        }

        public int ReservedArmies
        {
            set { reservedArmies = value; }
            get { return reservedArmies; }
        }

        public int PledgedArmies
        {
            set { pledgedArmies = value; }
            get { return pledgedArmies; }
        }

        public bool IsSafe(bot.BotState state)
        {
            bool safe = true;
            foreach (Region reg in Neighbors)
            {
                Region r = state.FullMap.GetRegion(reg.Id);
                if (!r.OwnedByPlayer(PlayerName)) safe = false;
            }
            return safe;
        }

        public bool DeployedOrTransferedThisTurn
        {
            set { deployedthisturn = value; }
            get { return deployedthisturn; }
        }

 //       public bool DeployedOrTransferedLastTurn
 //       {
 //           set { deployedlastturn = value; }
 //           get { return deployedlastturn; }
 //       }

        public bool HasNeighbour(int id)
        {
            foreach (Region neigh in neighbors)
            {
                if (neigh.Id == id) return true;
            }
            return false;
        }
    }

}