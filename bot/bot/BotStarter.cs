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
            // order the superregions by lower number of armies reward
            // they are the fastest to complete
            // you get income advantage if you finish them early on
            var lst = state.FullMap.SuperRegions.OrderBy(p => p.ArmiesReward).ToList();

            // order the list of possible starting picks by proximity to the best superregions
            foreach (Region a in state.PickableStartingRegions)
            {
                a.tempSortValue = 0;

                // check if region is neighboring a better superregion
                var na = a.Neighbors.OrderBy(p => lst.IndexOf(a.SuperRegion)).ToList();
                
                // if neighbouring a higher ranked superregion
                if (a.SuperRegion.Id != na[0].Id)
                {
                    a.tempSortValue = lst.Count - lst.IndexOf(a.SuperRegion);
                } else {
                    a.tempSortValue = lst.Count - lst.IndexOf(a.SuperRegion);
                }

            }
            var picks = state.PickableStartingRegions.OrderByDescending(p => p.tempSortValue).ToList();

            // assume opponent will also choose optimum picks
            // this will be useful later when we need to predict where opponent started
            foreach (Region reg in picks)
            {
                state.OpponentStartRegions.Add(reg);
            }

            return picks;
        }

        public List<DeployArmies> DeployBorderingEnemy(bot.BotState state, int armiesLeft)
        {
            List<DeployArmies> deployArmies = new List<DeployArmies>();
            if (state.EnemyBorders.Count > 0)
            {
                while (armiesLeft > 0)
                {
                    foreach (Region reg in state.EnemyBorders)
                    {
                        foreach (Region regn in reg.Neighbors)
                        {
                            Region rn = state.FullMap.GetRegion(regn.Id);
                            if (rn.OwnedByPlayer(state.MyPlayerName))
                            {
                                deployArmies.Add(new DeployArmies(state.MyPlayerName, rn, 1));
                                rn.PledgedArmies += 1;
                                armiesLeft--;
                                if (armiesLeft == 0) break;
                            }
                        }

                        if (armiesLeft == 0) break;
                    }
                }
            }
            return deployArmies;
        }


        public List<DeployArmies> DeployAtRandom(List<Region> list, BotState state, string myName, int armiesLeft) {
            List<DeployArmies> deployArmies = new List<DeployArmies>();
        
            while (armiesLeft > 0)
            {
                double rand = Random.NextDouble();
                int r = (int)(rand * list.Count);
                Region region = state.FullMap.GetRegion(list[r].Id);

                // validate if it's really our region
                if (region.OwnedByPlayer(myName))
                {
                    deployArmies.Add(new DeployArmies(myName, region, 1));
                    region.PledgedArmies += 1;
                    armiesLeft -= 1;
                }

            }
            return deployArmies;
        }

        public List<DeployArmies> FinishSuperRegion(BotState state, int armiesLeft)
        {
            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;

            List<DeployArmies> deployArmies = new List<DeployArmies>();
            
            //todo: later: calculate if we should be attacking a particular region more strongly (in case there is chance of counter or defensive positioning)

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

                // find neighbour with highest available armies (will be the attacker)
                foreach (Region an in region.Neighbors)
                {
                    Region a = state.FullMap.GetRegion(an.Id);
                    int aArmies = a.Armies + a.PledgedArmies - a.ReservedArmies;
                    if (!a.OwnedByPlayer(myName)) aArmies = -1;
                    an.tempSortValue = aArmies;
                }
                var neigh = region.Neighbors.OrderByDescending(p => p.tempSortValue).ToList();

                // make sure the attacking neighbour is owned by us, so we can deploy on it
                if (neigh[0].OwnedByPlayer(myName))
                {
                    int deployed = state.ScheduleNeutralAttack(neigh[0], region, armiesLeft);
                    if (deployed > 0)
                    {
                        deployArmies.Add(new DeployArmies(myName, neigh[0], deployed));
                        armiesLeft -= deployed;
                    }
                }
                else
                {
                    Console.Error.WriteLine("trying to attack out of a territory owned by " + neigh[0].PlayerName + "on round " + state.RoundNumber);
                }

            }

            if (armiesLeft < 0) Console.Error.WriteLine("exceeded army deployment on turn " + state.RoundNumber);

            // deploy the rest of our armies
            if (armiesLeft > 0)
            {
                if (state.EnemySighted)
                {
                    List<DeployArmies> placings = DeployBorderingEnemy(state, armiesLeft);
                    foreach (DeployArmies pl in placings)
                    {
                        deployArmies.Add(pl);
                    }
                }
                else
                {
                    // deploy the rest of our armies randomly

                    // do not deploy in areas that are safe
                    List<Region> list = new List<Region>();
                    foreach (Region reg in state.VisibleMap.Regions)
                    {
                        if (!reg.IsSafe(state)) list.Add(reg);
                    }

                    List<DeployArmies> placings = DeployAtRandom(list, state, myName, armiesLeft);
                    foreach (DeployArmies pl in placings)
                    {
                        deployArmies.Add(pl);
                    }

                    //todo: it would be better to give deployment priority to areas not bordering the expansion target we are trying to finish
                }

            }

            return deployArmies;
        }

        public List<DeployArmies> ExpandNormal(BotState state, int armiesLeft)
        {
            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;
            List<DeployArmies> deployArmies = new List<DeployArmies>();

            // deploy all on the two main expansion targets

            bool expanding = false;
            for (int i = 0; i < 2; i++)
            {
                foreach (Region reg in state.ExpansionTargets[i].SubRegions)
                {
                    Region region = state.FullMap.GetRegion(reg.Id);

                    // skip if you already own this region
                    if (region.OwnedByPlayer(myName)) continue;

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
                        deployArmies.Add(new DeployArmies(myName, neigh, deployed));
                        neigh.PledgedArmies += deployed;
                        armiesLeft -= deployed;
                        expanding = true;
                    }

                    // only do the expansion for the first neutral region found
                    break;

                }

                if (!expanding)
                {
                    foreach (Region r in state.VisibleMap.Regions)
                    {
                        if (r.OwnedByPlayer("neutral"))
                        {
                            foreach (Region rn in r.Neighbors)
                            {
                                Region rnn = state.FullMap.GetRegion(rn.Id);

                                if (rnn.OwnedByPlayer(myName))
                                {
                                    state.ScheduleFullAttack(rnn, r, armiesLeft);
                                    deployArmies.Add(new DeployArmies(myName, rnn, armiesLeft));
                                    armiesLeft = 0;
                                    expanding = true;
                                    break;
                                }
                            }

                        }

                        if (expanding) break;
                    }
                }

                //todo: later: if we are stuck in australia, enter breaker mode heading for south america through africa 

            }

            // deploy the rest of our armies randomly
            if (armiesLeft > 0)
            {
                // do not deploy in areas that are safe
                List<Region> list = new List<Region>();
                foreach (Region reg in state.VisibleMap.Regions)
                {
                    if (!reg.IsSafe(state)) list.Add(reg);
                }

                List<DeployArmies> placings = DeployAtRandom(list, state, myName, armiesLeft);
                foreach (DeployArmies pl in placings)
                {
                    deployArmies.Add(pl);
                }
            }

            //todo: later: need to decide if expansion should be done with stack or scatter, depending on how likely enemy is close

            return deployArmies;
        }

        public List<DeployArmies> ExpandMinimum(BotState state, int armiesLeft)
        {
            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;
            List<DeployArmies> deployArmies = new List<DeployArmies>();

            //todo: later: dont bother expanding on areas that might have enemy in a few turns

            // check if we can do minimum expansion on our best found expansion target
            bool doMinimumExpansion = true;
            foreach (Region reg in state.ExpansionTargets[0].SubRegions)
            {
                Region a = state.FullMap.GetRegion(reg.Id);

                // only try to expand if enemy is not on it
                if (a.OwnedByPlayer(opponentName)) doMinimumExpansion = false;

                // find best subregion to expand into, must be a neutral
                int count = 0;

                if (a.OwnedByPlayer(opponentName))
                {
                    a.tempSortValue = -5;
                    continue;
                }
                if (a.OwnedByPlayer(myName))
                {
                    a.tempSortValue = -10;
                    continue;
                }

                foreach (Region neigh in a.Neighbors)
                {
                    // if neighbor is the enemy, we shouldnt be thinking of expansion, we want to keep our stack close to the enemy
                    if (neigh.OwnedByPlayer(opponentName))
                    {
                        count -= 5;
                    }

                    // the more neighbours belong to me the better
                    if (neigh.OwnedByPlayer(myName))
                    {
                        count += 3;
                    }

                    // if it has neutrals on the target superregion its good
                    if (neigh.OwnedByPlayer("neutral") && (neigh.SuperRegion.Id == a.SuperRegion.Id)) count++;

                    // if it has unknowns on the target superregion its even better (means we will be able to finish it faster)
                    if (neigh.OwnedByPlayer("unknown") && (neigh.SuperRegion.Id == a.SuperRegion.Id)) count += 2;

                    // if it has only has 1 army it costs less to take, so its better
                    if (neigh.Armies == 1) count++;

                    // boost if this territory can take this neighbour without deploying, at all
                    int armyCount = a.Armies + a.PledgedArmies - a.ReservedArmies - neigh.Armies * 2;
                    // the more armies we'll have left the better
                    if (armyCount > 0) count += armyCount;
                }

                reg.tempSortValue = count;

            }

            if (doMinimumExpansion)
            {
                var lst = state.ExpansionTargets[0].SubRegions.OrderByDescending(p => p.tempSortValue).ToList();
                Region target = state.FullMap.GetRegion(lst[0].Id);

                if (target.OwnedByPlayer("neutral"))
                {

                    // find best region to attack from, must be my territory

                    foreach (Region a in lst[0].Neighbors)
                    {

                        // if it's not our territory, don't bother
                        if (!a.OwnedByPlayer(myName))
                        {
                            a.tempSortValue = -1;
                            continue;
                        }

                        // best attacker is the one with more armies available
                        int armyCount = a.Armies + a.PledgedArmies - a.ReservedArmies;
                        if (armyCount < 0) armyCount = 0;
                        a.tempSortValue = armyCount;

                    }
                    var atk = lst[0].Neighbors.OrderByDescending(p => p.tempSortValue).ToList();

                    Region attacker = state.FullMap.GetRegion(atk[0].Id);
                    if (attacker.OwnedByPlayer(myName))
                    {
                        int deployed = state.ScheduleNeutralAttack(attacker, target, armiesLeft);
                        deployArmies.Add(new DeployArmies(myName, attacker, deployed));
                        attacker.PledgedArmies += deployed;
                        armiesLeft -= deployed;
                    }
                    else
                    {
                        Console.Error.WriteLine("something went wrong with minimum expansion, tried to attack from a territory which wasnt mine");
                    }

                }
                else
                {
                    Console.Error.WriteLine("something went wrong with minimum expansion on round " + state.RoundNumber + " maybe because all options are bad?");
                }
            }
            else
            {
                //todo: deploy all on biggest stack of main area and attack if predicting to win

                //todo: later: find a better expansiontarget (without enemy), or risk finishing this one
            }

            return deployArmies;
        }

        public List<DeployArmies> AttackHard(BotState state, int armiesLeft)
        {
            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;
            List<DeployArmies> deployArmies = new List<DeployArmies>();

            // pick the target with most strategic value (position and stack advantage)
            foreach (Region rr in state.EnemyBorders)
            {
                rr.tempSortValue = 0;

                // give higher count if positioned in top ranked expansion
                if (rr.SuperRegion.ArmiesReward == 2) rr.tempSortValue += 2;

                // give higher count if we have stack advantage
                int maxarmies = 0;
                foreach (Region rn in rr.Neighbors)
                {
                    if (rn.OwnedByPlayer(myName))
                    {
                        if (rn.Armies > maxarmies) maxarmies = rn.Armies;
                    }
                }
                rr.tempSortValue += maxarmies - rr.Armies;
            }
            var lst = state.EnemyBorders.OrderByDescending(p => p.tempSortValue).ToList();
            Region target = state.FullMap.GetRegion(lst[0].Id);

            // pick the neighbour (our territory) with the highest army count
            foreach (Region rn in target.Neighbors)
            {
                int ac = 0;
                if (rn.OwnedByPlayer(myName))
                {
                    ac += rn.Armies;
                }
                rn.tempSortValue = ac;
            }
            lst = target.Neighbors.OrderByDescending(p => p.tempSortValue).ToList();
            Region attacker = lst[0];

            // validate
            if (target.OwnedByPlayer(opponentName) && attacker.OwnedByPlayer(myName))
            {
                deployArmies.Add(new DeployArmies(myName, attacker, armiesLeft));
                state.scheduledAttack.Add(new Tuple<int, int, int>(attacker.Id, target.Id, attacker.Armies - 1 + armiesLeft));
                armiesLeft = 0;
            }

            return deployArmies;
        }

        public List<DeployArmies> GetDeployArmiesMoves(BotState state, long timeOut)
        {
            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;
            bool enemySighted = state.EnemySighted;            
            List<DeployArmies> deployArmies = new List<DeployArmies>();
            int armiesLeft = state.StartingArmies;

            // while we are ozbased and have not breaken into brazil, focus entirely on that
            // exception if oz is threatened by enemy in siam, then it's not considered ozBased
            if (state.OZBased && !state.FullMap.GetRegion(12).OwnedByPlayer(myName))
            {
                // if we dont have brazil (12) but have north africa (21)
                if (!state.FullMap.GetRegion(12).OwnedByPlayer(myName) && state.FullMap.GetRegion(21).OwnedByPlayer(myName))
                {
                    deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(21), armiesLeft));
                    state.scheduledAttack.Add(new Tuple<int, int, int>(21, 12, state.FullMap.GetRegion(21).Armies - 1 + armiesLeft));
                    armiesLeft = 0;                           
                } else // we dont have north africa (21) but have egypt (22)
                if (!state.FullMap.GetRegion(21).OwnedByPlayer(myName) && state.FullMap.GetRegion(22).OwnedByPlayer(myName))
                {
                    deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(22), armiesLeft));
                    state.scheduledAttack.Add(new Tuple<int, int, int>(22, 21, state.FullMap.GetRegion(22).Armies - 1 + armiesLeft));
                    armiesLeft = 0;
                } else // we dont have egypt (22) but have middle east (36)
                if (!state.FullMap.GetRegion(22).OwnedByPlayer(myName) && state.FullMap.GetRegion(36).OwnedByPlayer(myName))
                {
                    deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(36), armiesLeft));
                    state.scheduledAttack.Add(new Tuple<int, int, int>(36, 22, state.FullMap.GetRegion(36).Armies - 1 + armiesLeft));
                    armiesLeft = 0;
                } else // we dont have middle east (36) but have india (37)
                if (!state.FullMap.GetRegion(36).OwnedByPlayer(myName) && state.FullMap.GetRegion(37).OwnedByPlayer(myName))
                {
                    deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(37), armiesLeft));
                    state.scheduledAttack.Add(new Tuple<int, int, int>(37, 36, state.FullMap.GetRegion(37).Armies - 1 + armiesLeft));
                    armiesLeft = 0;
                } else // we dont have india (37) but have siam (38)
                if (!state.FullMap.GetRegion(37).OwnedByPlayer(myName) && state.FullMap.GetRegion(38).OwnedByPlayer(myName))
                {
                    deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(38), armiesLeft));
                    state.scheduledAttack.Add(new Tuple<int, int, int>(38, 37, state.FullMap.GetRegion(38).Armies - 1 + armiesLeft));
                    armiesLeft = 0;
                } else // we dont have siam (38) but have indonesia (39)
                if (!state.FullMap.GetRegion(38).OwnedByPlayer(myName) && state.FullMap.GetRegion(39).OwnedByPlayer(myName))
                {
                    deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(39), armiesLeft));
                    state.scheduledAttack.Add(new Tuple<int, int, int>(39, 38, state.FullMap.GetRegion(39).Armies - 1 + armiesLeft));
                    armiesLeft = 0;
                }
                else
                {
                    Console.Error.WriteLine("ozbased turn without any action?! (round " + state.RoundNumber + ")");
                }

            } else if (enemySighted) {

                // early in the game bordering plenty of enemy areas
                if (((state.RoundNumber <= 2) && (state.EnemyBorders.Count > 1)) || (state.EnemyBorders.Count == 3))
                {
                    // if we have atleast 2 enemy sightings, pick one and hit it hard
                    List<DeployArmies> deploy = AttackHard(state, armiesLeft);
                    foreach (DeployArmies da in deploy)
                    {
                        deployArmies.Add(da);
                        armiesLeft -= da.Armies;
                    }

                    //todo: dont try to attack hard if we can predict that opponent could have another starting pick nearby
                    //todo: if this is the case, just deploy and sit, do min expansion elsewhere to get income advantage

                } else {

                    // figure out if the best listed superregion is finishable on this turn
                    bool finishableSuperRegion = false;
                    if (state.ExpansionTargets.Count > 0) finishableSuperRegion = state.FullMap.GetSuperRegion(state.ExpansionTargets[0].Id).IsFinishable(state.StartingArmies, myName);

                    if (finishableSuperRegion)
                    {
                        List<DeployArmies> deploy = FinishSuperRegion(state, armiesLeft);
                        foreach (DeployArmies da in deploy)
                        {
                            deployArmies.Add(da);
                            armiesLeft -= da.Armies;
                        }
                    } else { 
                        // do minimum expansion
                        List<DeployArmies> deploy = ExpandMinimum(state, armiesLeft);
                        foreach (DeployArmies da in deploy)
                        {
                            deployArmies.Add(da);
                            armiesLeft -= da.Armies;
                        }
                    }
                }

                //todo: later: decide if we should deploy all in one place and attack hard, or spread out the deployments on multiple borders and sit

                // deploy rest of your income bordering the enemy
                List<DeployArmies> placings = DeployBorderingEnemy(state, armiesLeft);
                foreach (DeployArmies da in placings)
                {
                    deployArmies.Add(da);
                    armiesLeft -= da.Armies;
                }

            }
            else
            {

                // figure out if the best listed superregion is finishable on this turn
                bool finishableSuperRegion = false;
                if (state.ExpansionTargets.Count > 0) finishableSuperRegion = state.FullMap.GetSuperRegion(state.ExpansionTargets[0].Id).IsFinishable(state.StartingArmies, myName);

                if (finishableSuperRegion)
                {
                    List<DeployArmies> deploy = FinishSuperRegion(state, armiesLeft);
                    foreach (DeployArmies da in deploy)
                    {
                        deployArmies.Add(da);
                        armiesLeft -= da.Armies;
                    }
                } else {
                    List<DeployArmies> expand = ExpandNormal(state, armiesLeft);
                    foreach (DeployArmies da in expand)
                    {
                        deployArmies.Add(da);
                        armiesLeft -= da.Armies;
                    }
                }
               
            }

            //todo: go through all deploy orders and condense them into one (to avoid multiple +1)

            return deployArmies;
        }

        /**
         * This method is called for at the second part of each round. This example attacks if a region has
         * more than 6 armies on it, and transfers if it has less than 6 and a neighboring owned region.
         * @return The list of DeployArmiess for one round
         */
        public List<AttackTransferMove> GetAttackTransferMoves(BotState state, long timeOut)
        {
            List<AttackTransferMove> attackTransferMoves = new List<AttackTransferMove>();
            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;

            // process already scheduled attacks (during the deployment phase)
            if (state.scheduledAttack.Count > 0)
            {
                foreach (Tuple<int, int, int> tup in state.scheduledAttack)
                {
                    Region from = state.FullMap.GetRegion(tup.Item1);
                    Region to = state.FullMap.GetRegion(tup.Item2);

                    // prevent from hitting a wall against opponent
                    if (to.OwnedByPlayer(opponentName) && (tup.Item3 <= to.Armies + state.EstimatedOpponentIncome))
                    {
                        Console.Error.WriteLine("prevent hitting a wall from " + from.Id + " to " + to.Id + " with " + tup.Item3 +  " armies on round " + state.RoundNumber);
                    } else {
                        attackTransferMoves.Add(new AttackTransferMove(myName, from, to, tup.Item3));
                    }
                }
            }

            foreach (Region re in state.VisibleMap.Regions)
            {
                Region fromRegion = state.FullMap.GetRegion(re.Id);

                if (fromRegion.OwnedByPlayer(myName))
                {      

                    bool borderingEnemy = false;
                    List<Region> enemyBorders = new List<Region>();
                    int enemyArmies = 0;
                    int estimatedOpponentIncome = state.EstimatedOpponentIncome;

                    foreach (Region reg in fromRegion.Neighbors)
                    {
                        Region region = state.FullMap.GetRegion(reg.Id);
                        if (reg.OwnedByPlayer(opponentName))
                        {
                            borderingEnemy = true;
                            enemyBorders.Add(region);
                            enemyArmies += region.Armies;
                        }
                    }
                    enemyBorders = enemyBorders.OrderBy(p => p.Armies).ToList();

                    int armiesLeft = fromRegion.Armies + fromRegion.PledgedArmies - fromRegion.ReservedArmies - 1;

                    //enemyArmies += estimatedOpponentIncome;

                    // if this region is bordering the enemy
                    if (borderingEnemy) {

                        // if multiple borders, attack small armies
                        if (enemyBorders.Count > 1)
                        {
                            foreach (Region en in enemyBorders)
                            {
                                // attack small armies with little armies
                                if ((en.Armies == 1) || (en.Armies == 2))
                                {
                                    if (armiesLeft > en.Armies * 2)
                                    {
                                        attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, en, en.Armies * 2));
                                        armiesLeft -= en.Armies * 2;
                                    }
                                }
                            }
                        }

                        // check if we can attack biggest stack with our own stack
                        Region enm = enemyBorders[enemyBorders.Count - 1];
                        if (armiesLeft > enm.Armies + estimatedOpponentIncome)
                        {
                            bool alreadyScheduled = false;
                            foreach (AttackTransferMove atk in attackTransferMoves)
                            {
                                // if it was already scheduled, upgrade it to use the remaining armies
                                if ((atk.FromRegion.Id == fromRegion.Id) && (atk.ToRegion.Id == enm.Id)){
                                    atk.Armies += armiesLeft;
                                    armiesLeft = 0;
                                    alreadyScheduled = true;
                                }
                            }

                            // if not previously scheduled then test attack fullforce
                            if (!alreadyScheduled) {
                                attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, enm, armiesLeft));
                                armiesLeft = 0;
                            }
                        }
                        

                        //todo: later: if we have multiple areas bordering an enemy, decide if we should attack with all or sit, delay a lot and attack with 2

                    } else {

                        // move leftovers to where they can border an enemy or finish the highest ranked expansion target superregion
                        if (armiesLeft > 0)
                        {
                            bool eborder = false;

                            // go through all the neighbours to see into which is more worth moving into
                            foreach (Region a in fromRegion.Neighbors)
                            {
                                Region an = state.FullMap.GetRegion(a.Id);
                                int count = 0;

                                // neighbour doesnt belong to us, can't move there
                                if (!an.OwnedByPlayer(myName))
                                {
                                    if (an.OwnedByPlayer(opponentName))
                                    {
                                        eborder = true;
                                    }

                                    a.tempSortValue = 0;
                                    continue;
                                }

                                // if not bordering an enemy
                                // move leftover to where they can border an enemy
                                // or finish the highest ranked expansion target superregion more easily

                                // we can also give a little bonus if it's bordering an area that will help finish the superregion
                                foreach (Region neigh in a.Neighbors)
                                {
                                    an = state.FullMap.GetRegion(neigh.Id);
                                    if (an.OwnedByPlayer(opponentName)) count += 10;
                                    if (an.OwnedByPlayer("neutral") && ((an.SuperRegion.Id == state.ExpansionTargets[0].Id) || (an.SuperRegion.Id == state.ExpansionTargets[1].Id))) count++;
                                }

                                a.tempSortValue = count;
                            }

                            var lst = fromRegion.Neighbors.OrderByDescending(p => p.tempSortValue).ToList();
                            Region dest = state.FullMap.GetRegion(lst[0].Id);
                            if (dest.OwnedByPlayer(state.MyPlayerName) && (!eborder))
                            {
                                attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, dest, armiesLeft));
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
            catch (Exception e) { 
                parser.Run(null);
            }
        }

    }
    
}