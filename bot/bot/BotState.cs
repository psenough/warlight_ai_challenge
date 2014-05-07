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
        public List<Tuple<int, int, int>> scheduledAttack; // attacker region id, target region id, armies to attack with

        private bool enemySighted;
        private List<Region> enemyBorders;
        private List<SuperRegion> ownedSR;

        private bool ozBased;
        private bool saBased;
        private bool africaBased;
        private int africaCount;

        private int estimatedOpponentIncome;

        private int hotStackZone;

        public BotState()
        {
            pickableStartingRegions = new List<Region>();
            opponentMoves = new List<Move>();

            opponentStartRegions = new List<Region>();
            expansionTargetSuperRegions = new List<SuperRegion>();
            this.scheduledAttack = new List<Tuple<int, int, int>>();

            enemySighted = false;
            enemyBorders = new List<Region>();
            ownedSR = new List<SuperRegion>();

            ozBased = false;
            saBased = false;
            africaBased = false;
            africaCount = 0;

            estimatedOpponentIncome = 5;

            hotStackZone = -1;

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
            foreach (Region r in fullMap.regions)
            {
                r.PlayerName = "unknown";
            }
            visibleMap = fullMap.GetMapCopy();
            for (int i = 1; i < mapInput.Length; i++)
            {
                try
                {
                    Region region = visibleMap.GetRegion(int.Parse(mapInput[i]));
                    String playerName = mapInput[i + 1];
                    int armies = int.Parse(mapInput[i + 2]);

                    region.PlayerName = playerName;
                    region.PreviousTurnArmies = region.Armies;
                    region.Armies = armies;
                    // clean up temporary variables
                    region.ReservedArmies = 0;
                    region.PledgedArmies = 0;
                    
                    // update fullmap aswell
                    Region region2 = fullMap.GetRegion(region.Id);
                    region2.PlayerName = playerName;
                    region2.PreviousTurnArmies = region2.Armies;
                    region2.Armies = armies;
                    region2.PledgedArmies = 0;
                    region2.ReservedArmies = 0;

                    i += 2;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Unable to parse Map Update " + e);
                }
            }
       
            enemySighted = false;
            enemyBorders.Clear();

            List<Region> unknownRegions = new List<Region>();

            foreach (var region in visibleMap.Regions)
            {
                // Remove regions from visible map which are unknown
                if (region.PlayerName == "unknown")
                    unknownRegions.Add(region);

                // Determing if there are any enemy sightings
                if (region.OwnedByPlayer(OpponentPlayerName))
                {
                    enemySighted = true;
                    enemyBorders.Add(region);
                }
            }

            // remove unknownRegions from visible map
            foreach (Region unknownRegion in unknownRegions)
                visibleMap.Regions.Remove(unknownRegion);

            scheduledAttack.Clear();

            ownedSR.Clear();
            foreach (SuperRegion sr in fullMap.superRegions) {
                sr.numberOfRegionsOwnedByUs = 0;
                sr.numberOfRegionsOwnedByOpponent = 0;
                sr.ownedByUs = false;
            }

            // check what superregions we own
            foreach (Region reg in visibleMap.regions)
            {
                SuperRegion par = fullMap.GetSuperRegion(reg.SuperRegion.Id);
                if (reg.OwnedByPlayer(myName)) par.numberOfRegionsOwnedByUs++;
                if (reg.OwnedByPlayer(opponentName)) par.numberOfRegionsOwnedByOpponent++;
            }
            foreach (SuperRegion sr in fullMap.superRegions) {
                if (sr.numberOfRegionsOwnedByUs == sr.SubRegions.Count) sr.ownedByUs = true;
            }

            // update list of opponent start regions
            UpdateOpponentStartRegions();

            ozBased = false;
            saBased = false;
            africaBased = false;

            // figure out best expansion target
            switch (RoundNumber) // start of round 1
            {
                case 1:
                    {
                        expansionTargetSuperRegions = FullMap.GetMapCopy().SuperRegions;

                        // sort super region by quality of expansibility
                        foreach (SuperRegion a in expansionTargetSuperRegions)
                        {
                            // superregion is considered safe if we have all the strategic starting picks (in, or neighbouring) the superregion
                            // this function quantifies how safe it really is

                            int count = 0;

                            bool redflag = false; //redflag when there is enemy or unknown on a starting pick in or neighbouring
                            foreach (Region reg in pickableStartingRegions)
                            {
                                //bool found = false;

                                // check if we have any neighbour bordering this superregion (this selection includes picks in or countering)
                                foreach (Region neighbour in reg.Neighbors)
                                {

                                    if (neighbour.SuperRegion.Id == a.Id)
                                    {
                                        //if it is neighboring an area of this superregion, check who the pick was assigned to
                                        switch (reg.PlayerName)
                                        {
                                            case "player1":
                                                if (myName == "player1")
                                                {
                                                    count += 2;
                                                }
                                                else
                                                {
                                                    redflag = true;
                                                }
                                                break;
                                            case "player2":
                                                if (myName == "player2")
                                                {
                                                    count += 2;
                                                }
                                                else
                                                {
                                                    redflag = true;
                                                }
                                                break;
                                            case "neutral":
                                                count++;
                                                break;
                                            case "unknown":
                                                //todo: check if the pick was picked by me in lower position then my last gotten pick
                                                //count--;
                                                //todo: instead of giving it lower priority, it could also be worth increasing the aggressivity
                                                count--;
                                                break;
                                        }

                                        // we only need to check one of the found neighbours to know this is a relevant pick, skip the rest
                                        //found = true;
                                        break;
                                    }
                                }

                                //if (found) break;

                            }

                            if (a.SubRegions.Count <= 4) count += 2;
                            else if (a.SubRegions.Count <= 5) count++;

                            if (redflag) count = -1;

                            a.tempSortValue = count;
                        }


                        expansionTargetSuperRegions = expansionTargetSuperRegions.OrderByDescending(p => p.tempSortValue).ToList();

                    }
                    break;
                default: // start of other rounds
                    {

                        // check if we are based somewhere
                        int ozCount = 0;
                        int saCount = 0;
                        africaCount = 0;
                        foreach (Region reg in FullMap.Regions)
                        {

                            // check if we are ozBased
                            // all 4 areas of oz are ours & siam is not enemy & brazil is not ours
                            // brazil is main target, if we have a foot there it doesnt matter if we are oz based or not
                            if (((reg.Id == 39) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 40) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 41) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 42) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 12) && (!reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 38) && (!reg.OwnedByPlayer(OpponentPlayerName)))
                                )
                            {
                                ozCount++;
                            }

                            //todo: refactor this to use the new SuperRegion.ownedByUs bool

                            // check if we are saBased
                            // all 4 areas of south america are ours
                            if (((reg.Id == 10) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 11) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 12) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 13) && (reg.OwnedByPlayer(MyPlayerName)))
                                )
                            {
                                saCount++;
                            }

                            // check if we are africaBased
                            // all 6 areas of africa are ours
                            if (((reg.Id == 21) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 22) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 23) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 24) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 25) && (reg.OwnedByPlayer(MyPlayerName))) ||
                                    ((reg.Id == 26) && (reg.OwnedByPlayer(MyPlayerName)))
                                )
                            {
                                africaCount++;
                            }
                        }
                        if (ozCount == 6) ozBased = true;
                        if (saCount == 4) saBased = true;
                        if (africaCount == 6) africaBased = true;

                        bool enemyOnAfrica = false;
                        for (int j = 21; j <= 26; j++)
                        {
                            Region reg = fullMap.GetRegion(j);
                            if (reg.OwnedByPlayer(opponentName)) enemyOnAfrica = true;
                        }


                        // update our expansion target
                        if (expansionTargetSuperRegions.Count > 0)
                        {
                            // make sure we are not still trying to expand on a superregion we already own
                            if (FullMap.GetSuperRegion(expansionTargetSuperRegions[0].Id).ownedByUs) expansionTargetSuperRegions.RemoveAt(0);

                            // figure out the next best thing
                            foreach (SuperRegion sr in expansionTargetSuperRegions)
                            {
                                int count = 0;

                                bool redflag = false; //redflag when there is enemy or too many unknowns
                                int unknowns = 0;
                                int mine = 0;
                                int neutrals = 0;

                                foreach (Region region in sr.SubRegions)
                                {
                                    Region reg = FullMap.GetRegion(region.Id);

                                    if (reg.OwnedByPlayer(OpponentPlayerName))
                                    {
                                        redflag = true;
                                        continue;
                                    }

                                    if (reg.OwnedByPlayer(MyPlayerName)) mine++;
                                    if (reg.OwnedByPlayer("unknown")) unknowns++;
                                    if (reg.OwnedByPlayer("neutral")) neutrals++;

                                    // check neighbours
                                    foreach (Region rn in region.Neighbors)
                                    {
                                        Region regn = FullMap.GetRegion(rn.Id);

                                        // more borders belonging to us the better
                                        if (regn.OwnedByPlayer(MyPlayerName)) count += 2;

                                        // if there is a border beloging to the enemy, it's not a very good area for expansion
                                        if (regn.OwnedByPlayer(OpponentPlayerName)) count -= 6;
                                    }

                                }

                                count += (sr.SubRegions.Count - unknowns); // the less unknowns ratio the better
                                count += mine * 3;
                                count += (12 - sr.SubRegions.Count) * 2; // less territories the better, easier to defend
                                count += sr.ArmiesReward; // more army rewards the better

                                //todo: later: fine tune this heuristic math

                                
                                // priority exceptions:
                                
                                // if we are ozbased and africa has enemies, main expansion target should be asia
                                if (sr.Id == 5) // asia 
                                {
                                    if ((OZBased) && (enemyOnAfrica)) count += 10;
                                }

                                // if we are ozbased and enemy is stuck in south america, finish up africa
                                if (sr.Id == 4) // africa
                                {
                                    if ((OZBased) && (!enemyOnAfrica)) count += 2;
                                }

                                // if we are sabased main expansion target should always be north america
                                // africa is too vulnerable!
                                if (sr.Id == 1) // north america
                                {
                                    if (SABased) count += 15;
                                }



                                if (redflag) count = -1;

                                sr.tempSortValue = count;
                            }

                            expansionTargetSuperRegions = expansionTargetSuperRegions.OrderByDescending(p => p.tempSortValue).ToList();

                        }



                    } break;
            }

            hotStackZone = -1;

        }

        // Parses a list of the opponent's moves every round. 
        // Clears it at the start, so only the moves of this round are stored.
        public void ReadOpponentMoves(String[] moveInput)
        {
            opponentMoves.Clear();
            int tempArmies = 0;
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
                        move = new DeployArmies(playerName, region, armies);
                        tempArmies += armies;
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

            // assume opponent has same armies as us 
            estimatedOpponentIncome = startingArmies;
 
            // if they deploy more, then they obviously have more
            if (tempArmies > estimatedOpponentIncome) estimatedOpponentIncome = tempArmies;

            //todo: later: update base estimate when we break an area the opponent likely has

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

        public void SetRoundNumber(string value)
        {
            roundNumber = int.Parse(value);
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
            // also remove neutral sightings in that region
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

        public int ScheduleNeutralAttack(Region attacker, Region target, int armiesAvailable)
        {
            int usedArmies = 0;

            // validate our inputs
            if (!attacker.OwnedByPlayer(MyPlayerName) || target.OwnedByPlayer("unknown") || target.OwnedByPlayer(MyPlayerName) || armiesAvailable < 0)
            {
                // there must have been an error somewhere on the algo
                Console.Error.WriteLine("trying to schedule a neutral attack with invalid inputs (on round " + RoundNumber + ")");
                return 0;
            }

            // armies needed to attack neutral
            int neededToAttack = target.Armies * 2;
            int neededToDeploy = neededToAttack - attacker.Armies + attacker.PledgedArmies - attacker.ReservedArmies + 1;

            if (neededToDeploy > armiesAvailable + 1) {
                // there must have been an error somewhere on the algo
                Console.Error.WriteLine("trying to schedule a neutral attack without enough armies to carry it through (on round " + RoundNumber + ")");
                return 0;
            }

            if (neededToDeploy < 0)
            {
                neededToDeploy = 0;
            }

            attacker.PledgedArmies += neededToDeploy;
            usedArmies += neededToDeploy;

            attacker.ReservedArmies += neededToAttack;
            scheduledAttack.Add(new Tuple<int, int ,int>(attacker.Id, target.Id, neededToAttack));     

            return usedArmies;
        }

        public void ScheduleFullAttack(Region attacker, Region target, int armiesDeployed)
        {
            // validate our inputs
            if (!attacker.OwnedByPlayer(MyPlayerName) || target.OwnedByPlayer("unknown") || target.OwnedByPlayer(MyPlayerName))
            {
                // there must have been an error somewhere on the algo
                Console.Error.WriteLine("trying to schedule a full attack with invalid inputs (on round " + RoundNumber + ")");
                return;
            }

            // count total armies available
            int totalArmies = attacker.Armies + attacker.PledgedArmies - attacker.ReservedArmies + armiesDeployed - 1;

            attacker.PledgedArmies += armiesDeployed;

            attacker.ReservedArmies += totalArmies;
            scheduledAttack.Add(new Tuple<int, int, int>(attacker.Id, target.Id, totalArmies));
        }

        public void ScheduleAttack(Region attacker, Region target, int deployArmies, int attackArmies)
        {
            // validate our inputs
            if (!attacker.OwnedByPlayer(MyPlayerName) || target.OwnedByPlayer("unknown") || target.OwnedByPlayer(MyPlayerName))
            {
                // there must have been an error somewhere on the algo
                Console.Error.WriteLine("trying to schedule an attack with invalid inputs (on round " + RoundNumber + ")");
                return;
            }

            attacker.PledgedArmies += deployArmies;

            attacker.ReservedArmies += attackArmies;
            scheduledAttack.Add(new Tuple<int, int, int>(attacker.Id, target.Id, attackArmies));
        }


        public List<Region> EnemyBorders
        {
            get { return enemyBorders; }
        }

        public bool EnemySighted
        {
            get { return enemySighted; }
        }
        
        public bool OZBased
        {
            get { return ozBased; }
        }

        public bool SABased
        {
            get { return saBased; }
        }

        public bool AfricaBased
        {
            get { return africaBased; }
        }

        public int AfricaCount
        {
            get { return africaCount; }
        }

        public int EstimatedOpponentIncome
        {
            get { return estimatedOpponentIncome; }
        }

        public int HotStackZone
        {
            set { hotStackZone = value; }
            get { return hotStackZone; }
        }

        public bool RegionBelongsToEnemySuperRegion(int id)
        {
            Region thisregion = fullMap.GetRegion(id);
            SuperRegion thissr = fullMap.GetSuperRegion(thisregion.SuperRegion.Id);

            foreach (Region reg in thissr.SubRegions)
            {
                Region rn = fullMap.GetRegion(reg.Id);
                if (rn.OwnedByPlayer(myName)) return false;
                if (rn.OwnedByPlayer("neutral")) return false;
            }

            return true;
        }

        public bool RegionBelongsToOurSuperRegion(int id)
        {
            Region thisregion = fullMap.GetRegion(id);
            SuperRegion thissr = fullMap.GetSuperRegion(thisregion.SuperRegion.Id);

            foreach (Region reg in thissr.SubRegions)
            {
                Region rn = fullMap.GetRegion(reg.Id);
                if (!rn.OwnedByPlayer(myName)) return false;
            }

            return true;
        }

        public bool RegionBordersOneOfOurOwnSuperRegions(int id)
        {
            Region thisregion = fullMap.GetRegion(id);

            foreach (Region ne in thisregion.Neighbors)
            {
                SuperRegion sr = fullMap.GetSuperRegion( ne.SuperRegion.Id );
                if (sr.ownedByUs) return true;
            }

            return false;
        }

        public int FindNextStep(int dest)
        {
            // reset
            foreach (Region reg in fullMap.regions)
            {
                reg.tempSortValue = 0;
            }

            // initial list
            List<Region> list = new List<Region>();
            foreach (Region reg in FullMap.GetRegion(dest).Neighbors)
            {
                list.Add(reg);
            }

            // first iteration
            int it = 1;

            while ( list.Count > 0 )
            {
                List<Region> newlist = new List<Region>();
                foreach (Region testRegion in list)
                {
                    Region reg = fullMap.GetRegion(testRegion.Id);
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
                            foreach(Region neigh in reg.Neighbors) {
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

    }


}