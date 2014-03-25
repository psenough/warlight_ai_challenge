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
        private String playerName;

        // temp variables
        private int reservedArmies; // reserved for an attack
        private int pledgedArmies; // pledged being added at this turn
        public List<Tuple<Region,int>> scheduledAttack; // target area, armies to attack with

        public Region(int id, SuperRegion superRegion)
        {
            this.id = id;
            this.superRegion = superRegion;
            this.neighbors = new List<Region>();
            this.playerName = "unknown";
            this.armies = 0;

            this.reservedArmies = 0;
            this.pledgedArmies = 0;
            this.scheduledAttack = new List<Tuple<Region,int>>();

            superRegion.AddSubRegion(this);
        }

        public Region(int id, SuperRegion superRegion, String playerName, int armies)
        {
            this.id = id;
            this.superRegion = superRegion;
            this.neighbors = new List<Region>();
            this.playerName = playerName;
            this.armies = armies;

            this.reservedArmies = 0;
            this.pledgedArmies = 0;
            this.scheduledAttack = new List<Tuple<Region, int>>();

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

    }

}