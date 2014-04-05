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
            bool enemySighted = state.EnemySighted;
            
            // figure out if the best listed superregion is finishable on this turn
            bool finishableSuperRegion = false;
            if (state.ExpansionTargets.Count > 0) finishableSuperRegion = state.FullMap.GetSuperRegion(state.ExpansionTargets[0].Id).IsFinishable(state.StartingArmies, myName);

            List<PlaceArmiesMove> placeArmiesMoves = new List<PlaceArmiesMove>();
            int armiesLeft = state.StartingArmies;

            if (finishableSuperRegion)
            {
                //todo: later: calculate if we should be attacking a particular region strongly (in case there is chance of counter or defensive positioning)

                // if you have enough income to finish an area this turn, deploy for it
                foreach (Region reg in state.ExpansionTargets[0].SubRegions)
                {
                    Region region = state.FullMap.GetRegion(reg.Id);

                    // skip if you already own this region
                    if (region.OwnedByPlayer(myName)) continue;

                    if (!region.OwnedByPlayer("neutral"))
                    {
                        Console.Error.WriteLine("trying to finish a FTB with " + region.PlayerName + " on it is a bit silly");
                        break;
                    }

                    List<Region> neigh = new List<Region>();
                    foreach (Region nn in region.Neighbors)
                    {
                        neigh.Add(state.FullMap.GetRegion(nn.Id));
                    }
                    foreach (Region a in neigh)
                    {
                        int aArmies = a.Armies + a.PledgedArmies - a.ReservedArmies;
                        if (!a.OwnedByPlayer(myName)) aArmies = -1;
                        a.tempSortValue = aArmies;
                    }

                    // find our neighbour with highest available armies
                    //neigh.Sort(new RegionsAvailableArmiesSorter(myName));
                    neigh = neigh.OrderByDescending(p => p.tempSortValue).ToList();


                    // make sure the attacking neighbour is owned by us, so we can deploy on it
                    if (neigh[0].OwnedByPlayer(myName))
                    {
                        int deployed = state.ScheduleNeutralAttack(neigh[0], reg, armiesLeft);
                        placeArmiesMoves.Add(new PlaceArmiesMove(myName, neigh[0], deployed));
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
                if ((state.RoundNumber < 2) && (state.EnemyBorders.Count > 1))
                {
                    // if we have atleast 2 enemy sightings, pick one and hit it hard
                    //if (state.EnemyBorders.Count > 1)
                    {

                        //todo: later: do better sort to pick the target with most strategic value (positional and stack advantage)

                        Region target = state.FullMap.GetRegion(state.EnemyBorders[Random.Next(state.EnemyBorders.Count)].Id);
                        target.Neighbors.Sort( new RegionsHigherArmiesInMyNameSorter(myName) );
                        Region attacker = target.Neighbors[0];

                        if (target.OwnedByPlayer(opponentName) && attacker.OwnedByPlayer(myName))
                        {
                            placeArmiesMoves.Add(new PlaceArmiesMove(myName, attacker, armiesLeft));
                            state.scheduledAttack.Add(new Tuple<int, int, int>(attacker.Id, target.Id, attacker.Armies - 1 + armiesLeft));
                            armiesLeft = 0;
                        }                        
                    }
                }
                else
                {

                    //todo: later: dont bother expanding on areas that might have enemy in a few turns

                    // do minimum expansion on our best found expansion target, only if enemy is not on it
                    bool doMinimumExpansion = true;
                    foreach (Region reg in state.ExpansionTargets[0].SubRegions)
                    {
                        Region region = state.FullMap.GetRegion(reg.Id);
                        if (region.OwnedByPlayer(opponentName)) doMinimumExpansion = false;
                    }

                    if (doMinimumExpansion)
                    {
                        // find best subregion to expand into, must be a neutral
                        state.ExpansionTargets[0].SubRegions.Sort(new RegionsMinimumExpansionSorter(myName, opponentName));
                        Region target = state.FullMap.GetRegion(state.ExpansionTargets[0].SubRegions[0].Id);

                        if (target.OwnedByPlayer("neutral"))
                        {

                            // find best region to attack from, must be my territory
                            state.ExpansionTargets[0].SubRegions[0].Neighbors.Sort(new RegionsBestExpansionAttackerSorter(myName));
                            Region attacker = state.FullMap.GetRegion(state.ExpansionTargets[0].SubRegions[0].Neighbors[0].Id);
                            if (attacker.OwnedByPlayer(myName))
                            {
                                int deployed = state.ScheduleNeutralAttack(attacker, target, armiesLeft);
                                placeArmiesMoves.Add(new PlaceArmiesMove(myName, attacker, deployed));
                                armiesLeft -= deployed;
                            }
                            else
                            {
                                Console.Error.WriteLine("something went wrong with minimum expansion, tried to attack from a territory which wasnt mine");
                            }

                        }
                        else
                        {
                            Console.Error.WriteLine("something went wrong with minimum expansion on round " + state.RoundNumber + ", tried to attack a non neutral");
                        }
                    }
                    else
                    {
                        //todo: deploy all on biggest stack of main area and attack if predicting to win

                        //todo: later: find a better expansiontarget (without enemy), or risk finishing this one
                    }
                }
                
                // deploy rest of your income bordering the enemy
                if (state.EnemyBorders.Count > 0)
                {
                    while (armiesLeft > 0)
                    {
                        foreach (Region reg in state.EnemyBorders)
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

                for (int i = 0; i < 2; i++)
                {
                    foreach (Region reg in state.ExpansionTargets[i].SubRegions)
                    {
                        Region region = state.FullMap.GetRegion(reg.Id);

                        // skip if you already own this region
                        if (region.OwnedByPlayer(myName)) continue;

                        if (!region.OwnedByPlayer("neutral"))
                        {
                            Console.Error.WriteLine("trying to finish a FTB with " + region.PlayerName + " in it, on round " + state.RoundNumber + " is a bit silly");
                            break;
                        }

                        // find our neighbour with highest available armies
                        foreach (Region a in region.Neighbors)
                        {
                            int aArmies = a.Armies + a.PledgedArmies - a.ReservedArmies;
                            if (!a.OwnedByPlayer(myName)) aArmies = -1;
                            a.tempSortValue = aArmies;
                        }
                        var lst = region.Neighbors.OrderByDescending(p => p.tempSortValue).ToList();
                        Region neigh = state.FullMap.GetRegion(lst[0].Id);

                        if (neigh.OwnedByPlayer(myName))
                        {
                            int deployed = state.ScheduleNeutralAttack(neigh, region, armiesLeft);
                            placeArmiesMoves.Add(new PlaceArmiesMove(myName, neigh, deployed));
                            armiesLeft -= deployed;
                        }

                        // only do the expansion for the first neutral region found
                        break;

                    }
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
                    //from.PledgedArmies = 0;
                    //from.ReservedArmies = 0;
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
                        Region region = state.FullMap.GetRegion(reg.Id);
                        if (reg.OwnedByPlayer(opponentName))
                        {
                            borderingEnemy = true;
                            enemyBorders.Add(region);
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
            //parser.Run(null);
            try
            {
                string[] lines = System.IO.File.ReadAllLines(@"C:\Users\filipecruz\Documents\warlight_ai_challenge\bot\test.txt");
                parser.Run(lines);
            }
            catch (Exception e) { parser.Run(null); }
        }

    }
    
}