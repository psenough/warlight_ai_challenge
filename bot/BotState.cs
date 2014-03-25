using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using main;
using move;

namespace bot
{

    public class BotState
    {

        private String myName = "";
        private String opponentName = "";

        private readonly Map fullMap = new Map(); // This map is known from the start, contains all the regions and how they are connected
        private Map visibleMap; // This map represents everything the player can see, updated at the end of each round.

        private List<Region> pickableStartingRegions; // 2 randomly chosen regions from each superregion are given, which the bot can chose to start with

        private List<Move> opponentMoves; // List of all the opponent's moves, reset at the end of each round

        private int startingArmies; // Number of armies the player can place on map

        private int roundNumber;



        private List<Region> opponentStartRegions;

        private List<SuperRegion> expansionTargetSuperRegions;


        public BotState()
        {
            pickableStartingRegions = new List<Region>();
            opponentMoves = new List<Move>();

            opponentStartRegions = new List<Region>();
            expansionTargetSuperRegions = new List<SuperRegion>();

            roundNumber = 0;
        }

        public void UpdateSettings(String key, String value)
        {
            if (key == "your_bot") // Bot's own name
                myName = value;
            else if (key == "opponent_bot") // Opponent's name
                opponentName = value;
            else if (key == "starting_armies")
            {
                startingArmies = int.Parse(value);
                roundNumber++; // Next round
            }
        }

        // Initial map is given to the bot with all the information except for player and armies info
        public void SetupMap(String[] mapInput)
        {
            int i, regionId, superRegionId, reward;

            if (mapInput[1] == "super_regions")
            {
                for (i = 2; i < mapInput.Length; i++)
                {
                    try
                    {
                        superRegionId = int.Parse(mapInput[i]);
                        i++;
                        reward = int.Parse(mapInput[i]);
                        fullMap.Add(new SuperRegion(superRegionId, reward));
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Unable to parse SuperRegions " + e);
                    }
                }
            }
            else if (mapInput[1] == "regions")
            {
                for (i = 2; i < mapInput.Length; i++)
                {
                    try
                    {
                        regionId = int.Parse(mapInput[i]);
                        i++;
                        superRegionId = int.Parse(mapInput[i]);
                        SuperRegion superRegion = fullMap.GetSuperRegion(superRegionId);
                        fullMap.Add(new Region(regionId, superRegion));
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Unable to parse Regions " + e);
                    }
                }
            }
            else if (mapInput[1] == "neighbors")
            {
                for (i = 2; i < mapInput.Length; i++)
                {
                    try
                    {
                        Region region = fullMap.GetRegion(int.Parse(mapInput[i]));
                        i++;
                        String[] neighborIds = mapInput[i].Split(',');
                        for (int j = 0; j < neighborIds.Length; j++)
                        {
                            Region neighbor = fullMap.GetRegion(int.Parse(neighborIds[j]));
                            region.AddNeighbor(neighbor);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Unable to parse Neighbors " + e);
                    }
                }
            }
        }

        // Regions from wich a player is able to pick his preferred starting regions
        public void SetPickableStartingRegions(String[] mapInput)
        {
            for (int i = 2; i < mapInput.Length; i++)
            {
                int regionId;
                try
                {
                    regionId = int.Parse(mapInput[i]);
                    Region pickableRegion = fullMap.GetRegion(regionId);
                    pickableStartingRegions.Add(pickableRegion);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Unable to parse pickable regions " + e);
                }
            }
        }

        // Visible regions are given to the bot with player and armies info
        public void UpdateMap(String[] mapInput)
        {
            visibleMap = fullMap.GetMapCopy();
            for (int i = 1; i < mapInput.Length; i++)
            {
                try
                {
                    Region region = visibleMap.GetRegion(int.Parse(mapInput[i]));
                    String playerName = mapInput[i + 1];
                    int armies = int.Parse(mapInput[i + 2]);

                    region.PlayerName = playerName;
                    region.Armies = armies;

                    // clean up temporary variables
                    region.ReservedArmies = 0;
                    region.PledgedArmies = 0;
                    region.scheduledAttack = new List<Tuple<Region, int>>();
                    
                    // update fullmap (fog of war)
                    Region region2 = fullMap.GetRegion(region.Id);
                    region2.PlayerName = playerName;
                    region2.Armies = armies;

                    if (expansionTargetSuperRegions.Count > 0)
                    {
                        Region region3 = fullMap.GetRegion(region.Id);
                        region3.PlayerName = playerName;
                        region3.Armies = armies;
                    }
                    if (expansionTargetSuperRegions.Count > 1)
                    {
                        Region region3 = fullMap.GetRegion(region.Id);
                        region3.PlayerName = playerName;
                        region3.Armies = armies;
                    }

                    i += 2;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Unable to parse Map Update " + e);
                }
            }
            List<Region> unknownRegions = new List<Region>();

            // Remove regions which are unknown.
            foreach (var region in visibleMap.Regions)
                if (region.PlayerName == "unknown")
                    unknownRegions.Add(region);
            foreach (Region unknownRegion in unknownRegions)
                visibleMap.Regions.Remove(unknownRegion);


            if (RoundNumber == 1) // start of round 1
            {
                UpdateOpponentStartRegions();

                expansionTargetSuperRegions = fullMap.SuperRegions;
                expansionTargetSuperRegions.Sort(new SuperRegionsExpansionTargetSorter(pickableStartingRegions, myName));
            }
            else // start of other rounds
            {
                // update our expansion target
                // make sure we are not trying to expand on a superregion we already own
                if (expansionTargetSuperRegions[0].OwnedByPlayer() == MyPlayerName)
                {
                    expansionTargetSuperRegions.RemoveAt(0);
                }

                //todo: later: make sure we are not trying to expand on a region where enemy is
                //todo: foolproof Project not to try to access expansionTargetSuperRegions[0] when it's empty
            }

        }

        // Parses a list of the opponent's moves every round. 
        // Clears it at the start, so only the moves of this round are stored.
        public void ReadOpponentMoves(String[] moveInput)
        {
            opponentMoves.Clear();
            for (int i = 1; i < moveInput.Length; i++)
            {
                try
                {
                    Move move;
                    if (moveInput[i + 1] == "place_armies")
                    {
                        Region region = visibleMap.GetRegion(int.Parse(moveInput[i + 2]));
                        String playerName = moveInput[i];
                        int armies = int.Parse(moveInput[i + 3]);
                        move = new PlaceArmiesMove(playerName, region, armies);
                        i += 3;
                    }
                    else if (moveInput[i + 1] == "attack/transfer")
                    {
                        Region fromRegion = visibleMap.GetRegion(int.Parse(moveInput[i + 2]));
                        if (fromRegion == null) // Might happen if the region isn't visible
                            fromRegion = fullMap.GetRegion(int.Parse(moveInput[i + 2]));

                        Region toRegion = visibleMap.GetRegion(int.Parse(moveInput[i + 3]));
                        if (toRegion == null) // Might happen if the region isn't visible
                            toRegion = fullMap.GetRegion(int.Parse(moveInput[i + 3]));

                        String playerName = moveInput[i];
                        int armies = int.Parse(moveInput[i + 4]);
                        move = new AttackTransferMove(playerName, fromRegion, toRegion, armies);
                        i += 4;
                    }
                    else
                    { // Never happens
                        continue;
                    }
                    opponentMoves.Add(move);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Unable to parse Opponent moves " + e);
                }
            }
        }

        public String MyPlayerName
        {
            get { return myName; }
        }

        public String OpponentPlayerName
        {
            get { return opponentName; }
        }

        public int StartingArmies
        {
            get { return startingArmies; }
        }

        public int RoundNumber
        {
            get { return roundNumber; }
        }

        public Map VisibleMap
        {
            get { return visibleMap; }
        }

        public Map FullMap
        {
            get { return fullMap; }
        }

        public List<Move> OpponentMoves
        {
            get { return opponentMoves; }
        }
        
        public List<Region> PickableStartingRegions
        {
            get { return pickableStartingRegions; }
        }



        

        public void UpdateOpponentStartRegions()
        {

            // regions to remove from OpponentStartRegions
            List<Region> remRegions = new List<Region>();

            // if there is a region on the opponent start regions list whose player is me on the current state
            // then that means i got the pick, so we can remove it from the prediction list
            foreach (Region reg in opponentStartRegions)
            {
                foreach (Region mapreg in VisibleMap.Regions)
                {
                    if (mapreg.Id == reg.Id)
                    {
                        switch (mapreg.PlayerName)
                        {
                            case "player1":
                                if (myName == "player1") remRegions.Add(reg);
                                else reg.PlayerName = "player1";
                                break;
                            case "player2":
                                if (myName == "player2") remRegions.Add(reg);
                                else reg.PlayerName = "player2";
                                break;
                            case "neutral":
                                remRegions.Add(reg);
                                break;
                            case "unknown":
                            default:
                                break;
                        }
                    }
                }
            }

            //todo: check if there is any "unknown" pick that was picked by us in a lower position then our last gotten pick                            

            foreach (Region remRegion in remRegions)
                opponentStartRegions.Remove(remRegion);
        }

        public List<Region> OpponentStartRegions
        {
            get { return opponentStartRegions; }
            set
            {
                opponentStartRegions = value;
            }
        }

        public List<SuperRegion> ExpansionTargets
        {
            get { return expansionTargetSuperRegions; }
        }

        public int ScheduleNeutralAttack(Region target, Region attacker, int armiesAvailable)
        {
            int usedArmies = 0;

            // armies needed to attack neutral
            int neededToAttack = target.Armies * 2;
            int neededToDeploy = neededToAttack - attacker.Armies + attacker.PledgedArmies - attacker.ReservedArmies;

            if (neededToDeploy > armiesAvailable) {
                // there must have been a mistake somewhere on the algo
                //return 0;
            } else {
                attacker.PledgedArmies += neededToDeploy;
                usedArmies += neededToDeploy;

                attacker.ReservedArmies += neededToAttack;
                attacker.scheduledAttack.Add(new Tuple<Region,int>(target, neededToAttack));
            }

            return usedArmies;
        }
    }

}