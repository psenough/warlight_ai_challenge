using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using main;
using move;

namespace bot
{

    public class BotStarter : Bot
    {
        public static Random Random = new Random();

        public List<Region> GetPreferredStartingRegions(BotState state, long timeOut)
        {
            // order the superregions by lower number of armies reward, they are the fastes to complete
            state.FullMap.SuperRegions.Sort(new SuperRegionsLowerArmiesSorter());

            // order the list of possible starting picks by proximity to the best superregions
            state.PickableStartingRegions.Sort(new RegionsImportanceSorter(state.FullMap.SuperRegions));

            // only return 6
            //state.PickableStartingRegions.RemoveRange(6, state.PickableStartingRegions.Length-6);

            // assume opponent will also choose optimum picks
            // this will be useful later when we need to predict where opponent started
            foreach (Region reg in state.PickableStartingRegions)
            {
                state.OpponentStartRegions.Add(new Region(reg.Id, new SuperRegion(reg.SuperRegion.Id, reg.SuperRegion.ArmiesReward)));
            }

            return state.PickableStartingRegions;
        }

        public List<PlaceArmiesMove> DeployAtRandom(BotState state, int armiesLeft) {
            List<PlaceArmiesMove> placeArmiesMoves = new List<PlaceArmiesMove>();
            String myName = state.MyPlayerName;
            var visibleRegions = state.VisibleMap.Regions;

            //int maxcycles = 20;
            //for (int i = 0; i < maxcycles; i++)
            //{
                while (armiesLeft > 0)
                {
                    double rand = Random.NextDouble();
                    int r = (int)(rand * visibleRegions.Count);
                    var region = visibleRegions.ElementAt(r);

                    if (region.OwnedByPlayer(myName))
                    {
                        placeArmiesMoves.Add(new PlaceArmiesMove(myName, region, 1));
                        armiesLeft -= 1;
                    }

                }

            //    if (armiesLeft <= 0) break;
            //}

            return placeArmiesMoves;
        }

        public List<PlaceArmiesMove> GetPlaceArmiesMoves(BotState state, long timeOut)
        {

            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;

            // figure out if the best listed superregion is finishable on this turn
            var finishableSuperRegion = state.ExpansionTargets[0].IsFinishable(state.StartingArmies, myName);

            // check if enemy is in sight, and where
            var enemySighted = false;
            List<Region> enemyBorders = new List<Region>();
            foreach (Region reg in state.VisibleMap.regions)
            {
                if (reg.OwnedByPlayer(state.OpponentPlayerName))
                {
                    enemySighted = true;
                    enemyBorders.Add(reg);
                }
            }

            List<PlaceArmiesMove> placeArmiesMoves = new List<PlaceArmiesMove>();
            int armiesLeft = state.StartingArmies;

            if (finishableSuperRegion)
            {
                //todo: later: calculate if we should be attacking a particular region strongly (in case there is chance of counter or defensive positioning)

                // if you have enough income to finish an area this turn, deploy for it
                foreach (Region reg in state.ExpansionTargets[0].SubRegions)
                {
                    
                    // skip if you already own this region
                    if (reg.OwnedByPlayer(myName)) continue;

                    if (!reg.OwnedByPlayer("neutral"))
                    {
                        Console.WriteLine("trying to finish a FTB with " + reg.PlayerName + " on it is a bit silly");
                        break;
                    }

                    // find our neighbour with highest available armies
                    reg.Neighbors.Sort(new RegionsAvailableArmiesSorter(myName));

                    // make sure the attacking neighbour is owned by us, so we can deploy on it
                    if (reg.Neighbors[0].OwnedByPlayer(myName))
                    {
                        int deployed = state.ScheduleNeutralAttack(reg.Neighbors[0], reg, armiesLeft);
                        placeArmiesMoves.Add(new PlaceArmiesMove(myName, reg.Neighbors[0], deployed));
                        armiesLeft -= deployed;
                    }
                   
                }

                if (armiesLeft < 0) Console.Error.WriteLine("exceeded army deployment on turn " + state.RoundNumber);
            
                // deploy the rest of our armies randomly
                if (armiesLeft > 0)
                {
                    List<PlaceArmiesMove> placings = DeployAtRandom(state, armiesLeft);
                    foreach (PlaceArmiesMove pl in placings)
                    {
                        placeArmiesMoves.Add(pl);
                    }

                    //todo: it would be better to give deployment priority to areas not bordering the expansion target we are trying to finish

                }

            }
            else if (enemySighted)
            {
                //todo: later: dont bother expanding on areas that might have enemy in a few turns

                // do minimum expansion on our best found expansion target

                // find best subregion to expand into, must be a neutral
                state.ExpansionTargets[0].SubRegions.Sort(new RegionsMinimumExpansionSorter(myName, opponentName));
                Region target = state.ExpansionTargets[0].SubRegions[0];

                if (target.OwnedByPlayer("neutral")) {
                    
                    // find best region to attack from, must be my territory
                    state.ExpansionTargets[0].SubRegions[0].Neighbors.Sort(new RegionsBestExpansionAttackerSorter(myName));
                    Region attacker = state.ExpansionTargets[0].SubRegions[0].Neighbors[0];
                    if (attacker.OwnedByPlayer(myName)) {
                        int deployed = state.ScheduleNeutralAttack( attacker, target, armiesLeft);
                        placeArmiesMoves.Add(new PlaceArmiesMove(myName, attacker, deployed));
                        armiesLeft -= deployed;
                    } else {
                        Console.Error.WriteLine("something went wrong with minimum expansion, tried to attack from a territory which wasnt mine");
                
                    }
                    
                } else {
                    Console.Error.WriteLine("something went wrong with minimum expansion, tried to attack a non neutral");
                }

                
                // deploy rest of your income bordering the enemy
                if (enemyBorders.Count > 0)
                {
                    while (armiesLeft > 0)
                    {
                        foreach (Region reg in enemyBorders)
                        {
                            foreach (Region regn in reg.Neighbors)
                            {
                                if (regn.OwnedByPlayer(myName))
                                {
                                    placeArmiesMoves.Add(new PlaceArmiesMove(myName, regn, 1));
                                    armiesLeft--;
                                    if (armiesLeft == 0) break;
                                }
                            }

                            if (armiesLeft == 0) break;
                        }
                    }
                }

                //todo: later: decide if we should deploy all in one place and attack hard, or spread out the deployments and sit

            }
            else
            {
                // deploy all on the two main expansion targets

                // do for first
                foreach (Region reg in state.ExpansionTargets[0].SubRegions)
                {

                    // skip if you already own this region
                    if (reg.OwnedByPlayer(myName)) continue;

                    if (!reg.OwnedByPlayer("neutral"))
                    {
                        Console.WriteLine("trying to finish a FTB with " + reg.PlayerName + " on it is a bit silly");
                        break;
                    }

                    // find our neighbour with highest available armies
                    reg.Neighbors.Sort(new RegionsAvailableArmiesSorter(myName));

                    // make sure the attacking neighbour is owned by us, so we can deploy on it
                    if (reg.Neighbors[0].OwnedByPlayer(myName))
                    {
                        int deployed = state.ScheduleNeutralAttack(reg.Neighbors[0], reg, armiesLeft);
                        placeArmiesMoves.Add(new PlaceArmiesMove(myName, reg.Neighbors[0], deployed));
                        armiesLeft -= deployed;
                    }

                    // only do the expansion for the first neutral region found
                    break; 

                }

                // do for second
                foreach (Region reg in state.ExpansionTargets[1].SubRegions)
                {

                    // skip if you already own this region
                    if (reg.OwnedByPlayer(myName)) continue;

                    if (!reg.OwnedByPlayer("neutral"))
                    {
                        Console.WriteLine("trying to finish a FTB with " + reg.PlayerName + " on it is a bit silly");
                        break;
                    }

                    // find our neighbour with highest available armies
                    reg.Neighbors.Sort(new RegionsAvailableArmiesSorter(myName));

                    // make sure the attacking neighbour is owned by us, so we can deploy on it
                    if (reg.Neighbors[0].OwnedByPlayer(myName))
                    {
                        int deployed = state.ScheduleNeutralAttack(reg.Neighbors[0], reg, armiesLeft);
                        placeArmiesMoves.Add(new PlaceArmiesMove(myName, reg.Neighbors[0], deployed));
                        armiesLeft -= deployed;
                    }

                    // only do the expansion for the first neutral region found
                    break;

                }

                // deploy the rest of our armies randomly
                if (armiesLeft > 0)
                {
                    List<PlaceArmiesMove> placings = DeployAtRandom(state, armiesLeft);
                    foreach (PlaceArmiesMove pl in placings)
                    {
                        placeArmiesMoves.Add(pl);
                    }
                }
             
                //todo: later: need to decide if expansion should be done with stack or scatter, depending on how likely enemy is close
            }

            return placeArmiesMoves;
        }

        /**
         * This method is called for at the second part of each round. This example attacks if a region has
         * more than 6 armies on it, and transfers if it has less than 6 and a neighboring owned region.
         * @return The list of PlaceArmiesMoves for one round
         */
        public List<AttackTransferMove> GetAttackTransferMoves(BotState state, long timeOut)
        {
          
            List<AttackTransferMove> attackTransferMoves = new List<AttackTransferMove>();
            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;

            // process already scheduled attacks (during the place armies phase)
            if (state.scheduledAttack.Count > 0)
            {
                foreach (Tuple<int, int, int> tup in state.scheduledAttack)
                {
                    Region from = state.FullMap.GetRegion(tup.Item1);
                    Region to = state.FullMap.GetRegion(tup.Item2);
                    attackTransferMoves.Add(new AttackTransferMove(myName, from, to, tup.Item3));
                }
            }

            foreach (Region fromRegion in state.VisibleMap.Regions)
            {
                if (fromRegion.OwnedByPlayer(myName))
                {      

                    bool borderingEnemy = false;
                    List<Region> enemyBorders = new List<Region>();

                    foreach (Region reg in fromRegion.Neighbors)
                    {
                        if (reg.OwnedByPlayer(opponentName))
                        {
                            borderingEnemy = true;
                            enemyBorders.Add(reg);
                        }
                    }

                    int armiesLeft = fromRegion.Armies + fromRegion.PledgedArmies - fromRegion.ReservedArmies - 1;

                    // if this region is bordering the enemy
                    if (borderingEnemy) {
                        // if their expected army count (current armies + half our current income) is lower then ours, attack
                        if (armiesLeft > enemyBorders[0].Armies + state.StartingArmies * .5)
                        {
                            attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, enemyBorders[0], armiesLeft));
                        }

                        //todo: refactor this code to handle attacking multiple enemy borders on same turn

                        //todo: later: if we have multiple areas bordering an enemy, decide if we should attack with all or sit, delay a lot and attack with 2

                    } else {
                        // move leftovers to where they can border an enemy or finish the highest ranked expansion target superregion
                        if (armiesLeft > 0)
                        {
                            fromRegion.Neighbors.Sort(new RegionsMoveLeftoversTargetSorter(myName, opponentName, state.ExpansionTargets[0].Id));
                            if (fromRegion.Neighbors[0].OwnedByPlayer(state.MyPlayerName))
                            {
                                attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, fromRegion.Neighbors[0], armiesLeft));
                            }
                        }
                    }

                }
            }
           
            return attackTransferMoves;
        }

        public static void Main(String[] args)
        {
            BotParser parser = new BotParser(new BotStarter());
            try
            {
                string[] lines = System.IO.File.ReadAllLines(@"C:\Users\filipecruz\Documents\Warlighter\test.txt");
                parser.Run(lines);
            }
            catch (Exception e) { parser.Run(null); }
        }

    }
    
}