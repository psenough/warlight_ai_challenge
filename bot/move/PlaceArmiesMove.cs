using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using main;

namespace move
{

/**
 * This Move is used in the first part of each round. It represents what Region is increased
 * with how many armies.
 */

	public class PlaceArmiesMove : Move {
		
		private Region region;
		private int armies;
		
		public PlaceArmiesMove(string playerName, Region region, int armies)
		{
			base.PlayerName = playerName;
			this.region = region;
			this.armies = armies;
		}

		public int Armies
		{
			set { armies = value; }
			get { return armies; }
		}

		public Region Region
		{
			get { return region; }
		}

		public String String
		{
			get { 
				if(string.IsNullOrEmpty(base.IllegalMove))
					return base.PlayerName + " place_armies " + region.Id + " " + armies;
				else
					return base.PlayerName + " illegal_move " + base.IllegalMove;
			}
		}
	}
}