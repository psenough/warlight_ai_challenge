using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace move
{

    public class Move
    {

        private String playerName; // Name of the player that did this move
        private String illegalMove = ""; // Gets the value of the error message if move is illegal, else remains empty

        public String PlayerName
        {
            set { playerName = value; }
            get { return playerName; }
        }

        public String IllegalMove
        {
            set { illegalMove = value; }
            get { return illegalMove; }
        }

    }
}