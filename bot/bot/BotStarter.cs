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

                int bestindex = lst.IndexOf(a.SuperRegion);
                int ni = lst.IndexOf(na[0].SuperRegion);
                if (ni < bestindex)
                {
                    bestindex = ni;

                    // better to have region inside than bordering
                    // counters in this map are hard due to starting armies being 2
                    // if we had more starting armies this value should be +=1
                    a.tempSortValue -= 1;
                }

                a.tempSortValue += lst.Count - bestindex;


                // little bonus for australia
                if (a.SuperRegion.Id == 6)
                {
                    a.tempSortValue += 2;
                }

                // little bonus for north africa
                if (a.Id == 21) a.tempSortValue++;

                // little bonus if it's bordering north africa
                foreach (Region nei in a.Neighbors)
                {
                    if (nei.Id == 21) a.tempSortValue++;
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
            String myName = state.MyPlayerName;
            List<DeployArmies> deployArmies = new List<DeployArmies>();

            if (state.EnemyBorders.Count > 0)
            {

                List<Region> listofregions = new List<Region>();

                foreach (Region reg in state.EnemyBorders)
                {
                    bool enemysr = state.FullMap.RegionBelongsToEnemySuperRegion(reg.Id, myName);

                    // let's assume enemy will never finish asia
                    // yes, it might be dangerous on games that drag out
                    // but a lot less dangerous then deploying in egypt and leaving north africa wide open on a regular basis
                    if (reg.SuperRegion.Id == 5) enemysr = false;

                    foreach (Region regn in reg.Neighbors)
                    {
                        Region rn = state.FullMap.GetRegion(regn.Id);
                        if (rn.OwnedByPlayer(myName))
                        {
                            int count = 1;

                            // if the threat is coming from a superregion the enemy owns
                            if (enemysr)
                            {
                                count += 10;
                            }

                            // if the threat is to a region belonging to a superregion we own
                            if (state.FullMap.RegionBelongsToOurSuperRegion(rn.Id, myName))
                            {
                                count += 12;
                            }

                            // if the threat is to a region bordering a superregion we own
                            if (state.FullMap.RegionBordersOneOfOurOwnSuperRegions(rn.Id))
                            {
                                count += 8;
                            }

                            // if the threat is to a region being double bordered by enemy
                            int countenemy = 0;
                            foreach (Region nnnn in rn.Neighbors)
                            {
                                Region nnn = state.FullMap.GetRegion(nnnn.Id);
                                if (nnn.OwnedByPlayer(state.OpponentPlayerName)) countenemy++;
                            }
                            if (countenemy > 1) count += 1;

                            // if the threat is to a crucial area like brazil or north africa, little boost
                            if ((rn.Id == 12) || (rn.Id == 21))
                            {
                                count++;
                            }

                            // the more armies it has the more it should be the source of our income
                            count += rn.Armies % 6;

                            rn.tempSortValue = count;

                            if (!listofregions.Contains(rn)) listofregions.Add(rn);
                        }
                    }
                }

                List<Region> lst = listofregions.OrderByDescending(p => p.tempSortValue).ToList();

                state.HotStackZone = lst[0].Id;


                // dispel hotstackzone if it has double the number of armies of all the enemy neighbours combined
                Region hreg = state.FullMap.GetRegion(state.HotStackZone);
                int enemycount = 0;
                foreach (Region regn in hreg.Neighbors)
                {
                    Region nn = state.FullMap.GetRegion(regn.Id);
                    if (nn.OwnedByPlayer(state.OpponentPlayerName))
                    {
                        enemycount += nn.Armies;
                    }
                }
                if (hreg.Armies > enemycount * 2) state.HotStackZone = -1;


                while (armiesLeft > 0)
                {
                    foreach (Region rn in lst)
                    {
                        // higher distribution to top of heap 
                        int rand = Random.Next(lst[0].tempSortValue + 1);
                        if (rand > rn.tempSortValue) continue;

                        // while we have armies left, use them
                        if (rn.OwnedByPlayer(state.MyPlayerName))
                        {
                            deployArmies.Add(new DeployArmies(state.MyPlayerName, rn, 1));
                            rn.PledgedArmies += 1;
                            armiesLeft--;
                            if (armiesLeft == 0) break;
                        }
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
            //string opponentName = state.OpponentPlayerName;

            List<DeployArmies> deployArmies = new List<DeployArmies>();
            if (state.ExpansionTargets.Count == 0) return deployArmies;
            
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
                    if (!a.OwnedByPlayer(myName)) {
                        an.tempSortValue = -1;
                    } else { 
                        // more armies available the better
                        int aArmies = a.Armies + a.PledgedArmies - a.ReservedArmies;
                        an.tempSortValue = aArmies;

                        // if that attacker has only one neutral of this superregion in sight, give it a little bonus
                        // this will help determine that this is the territory attacking when there are others with same armies available
                        int neutralcnt = 0;
                        foreach (Region un in a.Neighbors)
                        {
                            Region u = state.FullMap.GetRegion(un.Id);
                        
                            if (u.OwnedByPlayer("neutral") && (u.SuperRegion.Id == reg.Id)) neutralcnt++;
                        }
                        if (neutralcnt == 1) an.tempSortValue++;
                    }
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
                    Console.Error.WriteLine("trying to attack out of a territory owned by " + neigh[0].PlayerName + " on round " + state.RoundNumber + "! guess we ran out of available armies :(");
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

            }

            return deployArmies;
        }

        // more aggressive expansion, used only when game is stalled to help move things along for minimal expansion and finishregion to complete it
        public List<DeployArmies> ExpandGameStalled(BotState state, int armiesLeft)
        {
            string myName = state.MyPlayerName;
            //string opponentName = state.OpponentPlayerName;
            List<DeployArmies> deployArmies = new List<DeployArmies>();
            if (state.ExpansionTargets.Count == 0) return deployArmies;

            // expand on the main expansion target
            SuperRegion expansionTarget = state.FullMap.GetSuperRegion(state.ExpansionTargets[0].Id);
            foreach (Region reg in expansionTarget.SubRegions)
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
                    if (armiesLeft >= deployed) {
                        deployArmies.Add(new DeployArmies(myName, neigh, deployed));
                        //neigh.PledgedArmies += deployed;
                        armiesLeft -= deployed;
                    }
                }

                // only do the expansion for the first neutral region found
                //break;

            }

            return deployArmies;
        }


        // is only called when no enemy is in sight
        public List<DeployArmies> ExpandTowardsEnemyWithFullStack(BotState state, int armiesLeft)
        {
            string myName = state.MyPlayerName;
            //string opponentName = state.OpponentPlayerName;
            List<DeployArmies> deployArmies = new List<DeployArmies>();

            // determine the best region the opponent is most likely in
            if (state.OpponentStartRegions.Count > 0)
            {

                // find next step on shortest path to get there
                // and schedule an attack in that direction

                int nextstep = state.FullMap.FindNextStep(state.OpponentStartRegions[0].Id);
                if (nextstep != -1)
                {
                    Region region = state.FullMap.GetRegion(nextstep);

                    // find our neighbour with highest available armies
                    foreach (Region a in region.Neighbors)
                    {
                        int aArmies = a.Armies + a.PledgedArmies - a.ReservedArmies;
                        if (aArmies < 0) aArmies = 0;
                        if (!a.OwnedByPlayer(myName)) aArmies = -1;
                        a.tempSortValue = aArmies;
                    }
                    var lst = region.Neighbors.OrderByDescending(p => p.tempSortValue).ToList();
                    Region neigh = state.FullMap.GetRegion(lst[0].Id);
                    if (neigh.OwnedByPlayer(myName))
                    {
                        deployArmies.Add(new DeployArmies(myName, neigh, armiesLeft));
                        int armiesCount = neigh.Armies + neigh.PledgedArmies - neigh.ReservedArmies + armiesLeft - 1;
                        state.ScheduleAttack(neigh, region, armiesLeft, armiesCount );
                    }
                }
            }

            return deployArmies;
        }

        public List<DeployArmies> ExpandMinimum(BotState state, int armiesLeft)
        {
            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;
            List<DeployArmies> deployArmies = new List<DeployArmies>();
            if (state.ExpansionTargets.Count == 0) return deployArmies;

            // check if opponent might be there (or neighboring)
            // map has 12 starting spots, 3 of them are ours, 3 estimated for opponent
            // each gets removed as they are seen for the first time
            // so we can estimate decently the 
            // so only apply this filter between 9 and 6
            if ( state.OpponentStartRegions.Count > 6 )
            {
                for (int i = 0; i < 2; i++)
                {
                    foreach (Region reg in state.OpponentStartRegions[i].Neighbors)
                    {
                        Region neigh = state.FullMap.GetRegion(reg.Id);
                        if (neigh.SuperRegion.Id == state.ExpansionTargets[0].Id)
                        {
                            // don't do minimum expansion here
                            if (state.ExpansionTargets.Count == 0) return deployArmies;
                        }
                    }
                }
            }       

            // check if we can do minimum expansion on our best found expansion target
            bool doMinimumExpansion = true;
            foreach (Region reg in state.ExpansionTargets[0].SubRegions)
            {
                Region a = state.FullMap.GetRegion(reg.Id);

                // only try to expand if enemy is not on it
                if (a.OwnedByPlayer(opponentName)) doMinimumExpansion = false;

                // find best subregion to expand into
                int count = 0;

                // must be a neutral
                if (!a.OwnedByPlayer("neutral"))
                {
                    reg.tempSortValue = -1;
                    continue;
                }

                foreach (Region neig in a.Neighbors)
                {
                    Region neigh = state.FullMap.GetRegion(neig.Id);

                    // if neighbor is the enemy, we shouldnt be thinking of expansion, we want to keep our stack close to the enemy
                    if (neigh.OwnedByPlayer(opponentName))
                    {
                        count -= 5;
                    }

                    // the more neighbours belong to me the better
                    if (neigh.OwnedByPlayer(myName))
                    {
                        count += 5;
                    }

                    // if it has neutrals on the target superregion its good
                    if (neigh.OwnedByPlayer("neutral") && (neigh.SuperRegion.Id == a.SuperRegion.Id)) count++;

                    // if it has unknowns on the target superregion its even better (means we will be able to finish it faster)
                    if (neigh.OwnedByPlayer("unknown") && (neigh.SuperRegion.Id == a.SuperRegion.Id)) count += 1;

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

                    foreach (Region an in lst[0].Neighbors)
                    {
                        Region a = state.FullMap.GetRegion(an.Id);

                        // if it's not our territory, don't bother
                        if (!a.OwnedByPlayer(myName))
                        {
                            an.tempSortValue = -1;
                            continue;
                        }

                        // best attacker is the one with more armies available
                        int armyCount = a.Armies + a.PledgedArmies - a.ReservedArmies;
                        if (armyCount < 0) armyCount = 0;
                        an.tempSortValue = armyCount;

                    }
                    var atk = lst[0].Neighbors.OrderByDescending(p => p.tempSortValue).ToList();

                    Region attacker = state.FullMap.GetRegion(atk[0].Id);
                    if (attacker.OwnedByPlayer(myName))
                    {
                        int deployed = state.ScheduleNeutralAttack(attacker, target, armiesLeft);
                        deployArmies.Add(new DeployArmies(myName, attacker, deployed));
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
                state.ScheduleAttack(attacker, target, armiesLeft, attacker.Armies - 1 + armiesLeft);
                //state.scheduledAttack.Add(new Tuple<int, int, int>(attacker.Id, target.Id, attacker.Armies - 1 + armiesLeft));
                armiesLeft = 0;
            }

            return deployArmies;
        }

        public bool FinishableSuperRegion(BotState state)
        {
            string myName = state.MyPlayerName;
            int armiesLeft = state.StartingArmies;
            if (state.ExpansionTargets.Count == 0) return false;

            bool finishableSuperRegion = false;
            SuperRegion targetSR = state.FullMap.GetSuperRegion(state.ExpansionTargets[0].Id);
            if (state.ExpansionTargets.Count > 0)
            {
                finishableSuperRegion = targetSR.IsFinishable(armiesLeft, myName);
            }
            // it might be finishable but is it safe to finish without being a waste of armies?
            if (finishableSuperRegion)
            {
                if (!targetSR.IsSafeToFinish(state)) finishableSuperRegion = false;

                // africa without a foot in north africa is never safe to finish
                if ((targetSR.Id == 4) && !state.FullMap.GetRegion(21).OwnedByPlayer(myName)) finishableSuperRegion = false;
            }

            return finishableSuperRegion;
        }

        public List<DeployArmies> GetDeployArmiesMoves(BotState state, long timeOut)
        {
            string myName = state.MyPlayerName;
            string opponentName = state.OpponentPlayerName;
            bool enemySighted = state.EnemySighted;            
            List<DeployArmies> deployArmies = new List<DeployArmies>();
            int armiesLeft = state.StartingArmies;

            // figure out if the best listed superregion is finishable on this turn
            bool finishableSuperRegion = FinishableSuperRegion(state);

            if (enemySighted)
            {
                
                // on first rounds, attack any enemies on sight, unless we can finish a super region
                if ((state.RoundNumber < 5) && !finishableSuperRegion)
                {
                    // if we have atleast 2 enemy sightings, pick one and hit it hard
                    List<DeployArmies> deploy = AttackHard(state, armiesLeft);
                    foreach (DeployArmies da in deploy)
                    {
                        deployArmies.Add(da);
                        armiesLeft -= da.Armies;
                    }
                    
                }

                // if game is stuck with high stacks expand to get more income
                if (armiesLeft > 0)
                {
                    bool bigstack = false;
                    foreach (Region reg in state.VisibleMap.Regions)
                    {
                        if (reg.OwnedByPlayer(myName))
                        {
                            if (reg.Armies > 180)
                            {
                                bigstack = true;
                            }
                        }
                    }
                    if (bigstack)
                    {
                        List<DeployArmies> expand = ExpandGameStalled(state, armiesLeft);
                        foreach (DeployArmies da in expand)
                        {
                            deployArmies.Add(da);
                            armiesLeft -= da.Armies;
                        }
                    }
                }

                if (finishableSuperRegion)
                {
                    List<DeployArmies> deploy = FinishSuperRegion(state, armiesLeft);
                    foreach (DeployArmies da in deploy)
                    {
                        deployArmies.Add(da);
                        armiesLeft -= da.Armies;
                    }

                } else {

                    // used to have full attack against spotted enemy super region here, but it's now being taken care of during the next phase

                    // minimum expansion
                    if ((armiesLeft > 0) && (state.ExpansionTargets.Count > 0))
                    {
                        // do minimum expansion, but only on expansiontargets that are close to being finished
                        // or we know the game has been stalled for a while (stacks bigger then, lets say 50)
                        // or we have no superregion at all, so we really need one
                        //todo: and we are fairly certain the target sr is not already being bordered by enemy

                        // check how many territories of the expansion target we already own
                        int count = 0;
                        foreach (Region reg in state.ExpansionTargets[0].SubRegions)
                        {
                            Region rn = state.FullMap.GetRegion(reg.Id);
                            if (rn.OwnedByPlayer(myName)) count++;
                        }

                        bool issafe = true;
                        // check if there are any estimated opponentstartingregions in or bordering this superregion
                        // only check for south america and australia, rest can be considered safe by default
                        if (state.ExpansionTargets[0].ArmiesReward < 3) { 
                            foreach (Region border in state.OpponentStartRegions)
                            {
                                foreach (Region n in border.Neighbors)
                                {
                                    Region b = state.FullMap.GetRegion(n.Id);
                                    if (b.SuperRegion.Id == state.ExpansionTargets[0].Id) issafe = false;
                                }
                            }
                        }

                        // check if the game is starting to have big stacks
                        bool bigstack = false;
                        foreach (Region reg in state.VisibleMap.Regions)
                        {
                            if (reg.OwnedByPlayer(myName))
                            {
                                if (reg.Armies > 50)
                                {
                                    bigstack = true;
                                    
                                    //todo: test this below...
                                    // check if we are building a bigstack in middle of africa without having eyes on SA
                                    if (state.FullMap.GetRegion(12).OwnedByPlayer("unknown") && state.FullMap.GetRegion(21).OwnedByPlayer(opponentName))
                                    {
                                        //todo: find the shortest neutral / unknown / low enemy army count path there, needs to be less then 3 steps max

                                        //todo: schedule our way there
                                    }
                                }
                            }
                        }

                        // do minimum expansion
                        if ((armiesLeft > 0) && issafe) {
                            if ((count > state.ExpansionTargets[0].SubRegions.Count * 0.5) || (bigstack) || (state.StartingArmies == 5))
                            {
                                List<DeployArmies> deploy = ExpandMinimum(state, armiesLeft);
                                foreach (DeployArmies da in deploy)
                                {
                                    deployArmies.Add(da);
                                    armiesLeft -= da.Armies;
                                }
                            }
                        }
                    }

                }
                
                // deploy rest of your income bordering the enemy
                List<DeployArmies> placings = DeployBorderingEnemy(state, armiesLeft);
                foreach (DeployArmies da in placings)
                {
                    deployArmies.Add(da);
                    armiesLeft -= da.Armies;
                }

            } else {

                // no enemy in sight, expand normally / strategically

                if (finishableSuperRegion)
                {
                    List<DeployArmies> deploy = FinishSuperRegion(state, armiesLeft);
                    foreach (DeployArmies da in deploy)
                    {
                        deployArmies.Add(da);
                        armiesLeft -= da.Armies;
                    }

                } else {

                    if (state.OZBased)
                    {
                        // if we are based in australia and have no territory in africa, europe or south america
                        // we should be making our way into africa

                        bool thereyet = false;
                        foreach (Region reg in state.VisibleMap.Regions)
                        {
                            if ((reg.SuperRegion.Id == 2) || //SA
                                (reg.SuperRegion.Id == 3) || //europe
                                (reg.SuperRegion.Id == 4)) //africa
                            {
                                thereyet = true;
                            }
                        }

                        if (!thereyet)
                        {
                            // we dont have egypt (22) but have middle east (36)
                            if (!state.FullMap.GetRegion(22).OwnedByPlayer(myName) && state.FullMap.GetRegion(36).OwnedByPlayer(myName))
                            {
                                deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(36), armiesLeft));
                                state.ScheduleAttack(state.FullMap.GetRegion(36), state.FullMap.GetRegion(22), armiesLeft, state.FullMap.GetRegion(36).Armies - 1 + armiesLeft);
                                //state.scheduledAttack.Add(new Tuple<int, int, int>(36, 22, state.FullMap.GetRegion(36).Armies - 1 + armiesLeft));
                                armiesLeft = 0;
                            }
                            else // we dont have middle east (36) but have india (37)
                                if (!state.FullMap.GetRegion(36).OwnedByPlayer(myName) && state.FullMap.GetRegion(37).OwnedByPlayer(myName))
                                {
                                    deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(37), armiesLeft));
                                    state.ScheduleAttack(state.FullMap.GetRegion(37), state.FullMap.GetRegion(36), armiesLeft, state.FullMap.GetRegion(37).Armies - 1 + armiesLeft);
                                    //state.scheduledAttack.Add(new Tuple<int, int, int>(37, 36, state.FullMap.GetRegion(37).Armies - 1 + armiesLeft));
                                    armiesLeft = 0;
                                }
                                else // we dont have india (37) but have siam (38)
                                    if (!state.FullMap.GetRegion(37).OwnedByPlayer(myName) && state.FullMap.GetRegion(38).OwnedByPlayer(myName))
                                    {
                                        deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(38), armiesLeft));
                                        state.ScheduleAttack(state.FullMap.GetRegion(38), state.FullMap.GetRegion(37), armiesLeft, state.FullMap.GetRegion(38).Armies - 1 + armiesLeft);
                                        //state.scheduledAttack.Add(new Tuple<int, int, int>(38, 37, state.FullMap.GetRegion(38).Armies - 1 + armiesLeft));
                                        armiesLeft = 0;
                                    }
                                    else // we dont have siam (38) but have indonesia (39)
                                        if (!state.FullMap.GetRegion(38).OwnedByPlayer(myName) && state.FullMap.GetRegion(39).OwnedByPlayer(myName))
                                        {
                                            deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(39), armiesLeft));
                                            state.ScheduleAttack(state.FullMap.GetRegion(39), state.FullMap.GetRegion(38), armiesLeft, state.FullMap.GetRegion(39).Armies - 1 + armiesLeft);
                                            //state.scheduledAttack.Add(new Tuple<int, int, int>(39, 38, state.FullMap.GetRegion(39).Armies - 1 + armiesLeft));
                                            armiesLeft = 0;
                                        }
                                        else
                                        {
                                            Console.Error.WriteLine("ozbased turn without any action?! (round " + state.RoundNumber + ")");
                                        }
                        }
                        else
                        {
                            // lets go check north africa 
                            Region northafrica = state.FullMap.GetRegion(21);
                            if (northafrica.OwnedByPlayer("unknown") || northafrica.OwnedByPlayer("neutral"))
                            {
                                int nextstep = state.FullMap.FindNextStep(12);
                                if (nextstep != -1)
                                {
                                    Region region = state.FullMap.GetRegion(nextstep);

                                    // find our neighbour with highest available armies
                                    foreach (Region a in region.Neighbors)
                                    {
                                        int aArmies = a.Armies + a.PledgedArmies - a.ReservedArmies;
                                        if (aArmies < 0) aArmies = 0;
                                        if (!a.OwnedByPlayer(myName)) aArmies = -1;
                                        a.tempSortValue = aArmies;
                                    }
                                    var lst = region.Neighbors.OrderByDescending(p => p.tempSortValue).ToList();
                                    Region neigh = state.FullMap.GetRegion(lst[0].Id);
                                    if (neigh.OwnedByPlayer(myName))
                                    {
                                        deployArmies.Add(new DeployArmies(myName, neigh, armiesLeft));
                                        state.ScheduleAttack(neigh, region, armiesLeft, neigh.Armies + armiesLeft - 1);
                                        armiesLeft = 0;
                                    }
                                }
                            }

                            //todo: we have foot in africa/europe/south america but enemy could be expanding safely on north america...

                            //todo: how can we be sure we are not wasting armies going to alaska? need to track historic of enemy deployments...

                        }
                    }

                    if (state.AfricaBased) {
                        // if africaBased and not saBased, go hard into brazil
                        if (!state.SABased)
                        {
                            Region rnn = state.FullMap.GetRegion(21); // deploy all on north africa
                            Region r = state.FullMap.GetRegion(12); // attack brazil
                            state.ScheduleFullAttack(rnn, r, armiesLeft);
                            deployArmies.Add(new DeployArmies(myName, rnn, armiesLeft));
                            armiesLeft = 0;

                        } 
                        else if (state.FullMap.GetRegion(12).OwnedByPlayer(myName))
                        {
                            // we have brazil and no enemy bordering, so we can predict enemy is coming at us from oz
                            // so go hard into middle east
                            Region rnn = state.FullMap.GetRegion(22); // deploy all on egypt
                            Region r = state.FullMap.GetRegion(36); // attack middle east
                            if (!r.OwnedByPlayer(myName))
                            {
                                state.ScheduleFullAttack(rnn, r, armiesLeft);
                                deployArmies.Add(new DeployArmies(myName, rnn, armiesLeft));
                                armiesLeft = 0;
                            }
                        }
                    }

                    // if saBased and no enemy on north africa, go hard into north africa from brazil
                    if (state.SABased && !state.FullMap.GetRegion(21).OwnedByPlayer(opponentName))
                    {
                        Region rnn = state.FullMap.GetRegion(12); // deploy all on brazil
                        Region r = state.FullMap.GetRegion(21); // attack north africa
                        if (!r.OwnedByPlayer(myName))
                        {
                            state.ScheduleFullAttack(rnn, r, armiesLeft);
                            deployArmies.Add(new DeployArmies(myName, rnn, armiesLeft));
                            armiesLeft = 0;
                        }
                    }

                    if (state.SABased && state.FullMap.GetRegion(21).OwnedByPlayer(myName) && !state.NABased)
                    {
                        // do minimum expansion into north america
                        // expansion target 0 should already be pointing at it
                        if (armiesLeft > 0)
                        {                            
                            List<DeployArmies> deploy = ExpandMinimum(state, armiesLeft);
                            foreach (DeployArmies da in deploy)
                            {
                                deployArmies.Add(da);
                                armiesLeft -= da.Armies;
                            }
                        }

                        // deploy rest on North Africa
                        if (armiesLeft > 0) { 
                            deployArmies.Add(new DeployArmies(myName, state.FullMap.GetRegion(21), armiesLeft));
                            armiesLeft = 0;
                        }
                    }

                }

                // if no priority predictions occurs
                if (armiesLeft > 0)
                {
                    List<DeployArmies> expand = ExpandTowardsEnemyWithFullStack(state, armiesLeft);
                    foreach (DeployArmies da in expand)
                    {
                        deployArmies.Add(da);
                        armiesLeft -= da.Armies;
                    }
                }

            }

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
                    int armyCount = tup.Item3;

                    attackTransferMoves.Add(new AttackTransferMove(myName, from, to, armyCount, 4));
                }
            }

            Region northafrica = state.FullMap.GetRegion(21);

            foreach (Region re in state.VisibleMap.Regions)
            {
                Region fromRegion = state.FullMap.GetRegion(re.Id);

                if (fromRegion.OwnedByPlayer(myName))
                {
                    bool borderingEnemy = false;
                    List<Region> enemyBorders = new List<Region>();
                    int estimatedOpponentIncome = state.EstimatedOpponentIncome;

                    foreach (Region reg in fromRegion.Neighbors)
                    {
                        Region region = state.FullMap.GetRegion(reg.Id);
                        if (region.OwnedByPlayer(opponentName))
                        {
                            borderingEnemy = true;
                            enemyBorders.Add(region);
                        }
                    }
                    enemyBorders = enemyBorders.OrderBy(p => p.Armies).ToList();

                    int armiesLeft = fromRegion.Armies + fromRegion.PledgedArmies - fromRegion.ReservedArmies - 1;

                    // if we are neighboring a neutral north africa, go there
                    if (fromRegion.HasNeighbour(21) && northafrica.OwnedByPlayer("neutral") && (state.StartingArmies > 5) && (armiesLeft > 4))
                    {
                        attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, northafrica, armiesLeft, 4));
                        fromRegion.ReservedArmies += armiesLeft;
                        armiesLeft = 0;
                    }

                    // if this region is bordering the enemy
                    if (borderingEnemy) {

                        // attack regions that have small number of armies with small number of armies, just to clear them out
                        foreach (Region enmm in enemyBorders)
                        {
                            Region en = state.FullMap.GetRegion(enmm.Id);

                            // only do small attacks when we are bordering multiple enemies
                            // or target hasn't changed it's income on last turn and didn't receive any deploys/transfers either
                            // or it's in another superregion

                            if ((enemyBorders.Count > 2) || ((en.Armies == en.PreviousTurnArmies) && (!en.DeployedOrTransferedThisTurn) && (enemyBorders.Count > 2)))
                            {
                                // attack small armies with little armies
                                if ((en.Armies == 1) || (en.Armies == 2))
                                {
                                    if (armiesLeft > en.Armies * 2)
                                    {
                                        attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, en, en.Armies * 2, 3));
                                        armiesLeft -= en.Armies * 2;
                                        fromRegion.ReservedArmies += en.Armies * 2;
                                    }
                                }
                            }

                        }

                        // move any remaining armies to hotzonestack
                        // this will make sure you're moving stacks back to important defensive position
                        // and also help merge stacks when double bordering enemy
                        if ((state.HotStackZone != -1) && (armiesLeft > 0))
                        {
                            foreach (Region reg in fromRegion.Neighbors)
                            {
                                if (reg.Id == state.HotStackZone)
                                {
                                    attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, state.FullMap.GetRegion(reg.Id), armiesLeft, 6));
                                    fromRegion.ReservedArmies += armiesLeft;
                                    armiesLeft = 0;
                                }
                            }
                        }

                        // do a sweep to second degree neighbors to also move them towards stack
                        // needs to be in a different loop from first degree sweep, otherwise it would steal priority from moving into hotstockzone directly due to multiple neighboring
                        if ((state.HotStackZone != -1) && (armiesLeft > 0) && (fromRegion.Id != state.HotStackZone))
                        {
                            foreach (Region reg in fromRegion.Neighbors)
                            {
                                foreach (Region reg2 in reg.Neighbors)
                                {
                                    if (reg2.Id == state.HotStackZone)
                                    {
                                        attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, state.FullMap.GetRegion(reg.Id), armiesLeft, 6));
                                        fromRegion.ReservedArmies += armiesLeft;
                                        armiesLeft = 0;
                                    }
                                }
                            }
                        }


                        //todo: enemy stack could be hidden and moving in, we can use history to check
                        
                        
                        // divide stack armies on planned attacks
                        foreach (Region en in enemyBorders) {
                            Region enm = state.FullMap.GetRegion(en.Id);
                            
                            // determine how much is needed to not hit a wall
                            int nstackcount = 0;
                            foreach (Region reg in en.Neighbors)
                            {
                                Region regn = state.FullMap.GetRegion(reg.Id);
                                if (regn.OwnedByPlayer(state.OpponentPlayerName))
                                {
                                    nstackcount += regn.Armies - 1;
                                }
                            }
                            enm.tempSortValue = nstackcount; // store this value, we will use it further down on another enemyBorders cycle
                            int needed = (int)Math.Ceiling((enm.Armies + estimatedOpponentIncome + nstackcount) * 1.15f);
                            int max = (int)Math.Ceiling((enm.Armies + estimatedOpponentIncome + nstackcount) * 2.0f);

                            // if it's bordering a suspected superregion
                            // or there is only one enemyborder
                            // we should be hitting it and it alone fullforce
                            //todo: unless the border to super region is actually a double border (like middle east)
                            if (state.FullMap.RegionBelongsToEnemySuperRegion(enm.Id, myName) || (enemyBorders.Count == 1))
                            {
                                
                                bool alreadyScheduled = false;

                                // check if it's already scheduled
                                foreach (AttackTransferMove atk in attackTransferMoves)
                                {
                                    // if it was already scheduled buff it to use all armies
                                    if ((atk.FromRegion.Id == fromRegion.Id) && (atk.ToRegion.Id == enm.Id)){
                                        alreadyScheduled = true;
                                        break;
                                    }
                                }

                                // if not previously scheduled then try to schedule it with whatever we have
                                if ((!alreadyScheduled) && (armiesLeft > 0))
                                {
                                    int used = armiesLeft;
                                    if (armiesLeft > max) armiesLeft = max;
                                    attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, enm, used, 5));
                                    if (armiesLeft >= max) attackTransferMoves[attackTransferMoves.Count - 1].Locked = true;
                                    fromRegion.ReservedArmies += used;
                                    armiesLeft -= used;
                                }
                            }
                        }

                        // if we have armiesleft at this point it means we are not threatening a superregion or have multiple borders
                        // we should distribute the rest of the armies evenly
                        if (armiesLeft > 0)
                        {
                            // make sure all of them are scheduled
                            foreach (Region en in enemyBorders)
                            {
                                Region enm = state.FullMap.GetRegion(en.Id);

                                // check if it's already scheduled
                                bool alreadyScheduled = false;
                                foreach (AttackTransferMove atk in attackTransferMoves)
                                {
                                    // if it was already scheduled buff it to use all armies
                                    if ((atk.FromRegion.Id == fromRegion.Id) && (atk.ToRegion.Id == enm.Id))
                                    {
                                        alreadyScheduled = true;
                                        break;
                                    }
                                }

                                // if not previously scheduled then try to schedule it with single troop
                                if ((!alreadyScheduled) && (armiesLeft > 0))
                                {
                                    attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, enm, 1, 5));
                                    armiesLeft -= 1;
                                    fromRegion.ReservedArmies += 1;
                                }
                                
                            }

                            // try to buff them up evenly with whatever we have left
                            bool allLocked = false;
                            bool gotany = true;
                            while ((armiesLeft > 0) && (!allLocked) && (gotany))
                            {
                                gotany = false;
                                int biggestenemystack = -1;
                                foreach (Region en in enemyBorders)
                                {
                                    Region enm = state.FullMap.GetRegion(en.Id);

                                    // determine how much is needed to not hit a wall
                                    int nstackcount = enm.tempSortValue;
                                    int needed = (int)Math.Ceiling((enm.Armies + estimatedOpponentIncome + nstackcount) * 1.15f);
                                    int max = (int)Math.Ceiling((enm.Armies + estimatedOpponentIncome + nstackcount) * 2.0f);
 
                                    // divide it equally amongst the different targets

                                    // but give a little advantage against the highest stack
                                    if (biggestenemystack < needed) biggestenemystack = needed;
                           
                                    // buff already scheduled
                                    bool testLock = true;

                                    foreach (AttackTransferMove atk in attackTransferMoves)
                                    {
                                        // buff it to use more armies until it's reached max
                                        // don't buff if it's a small army attack (which always has priority of 3)
                                        if ((atk.FromRegion.Id == fromRegion.Id) && (atk.ToRegion.Id == enm.Id) && (atk.Priority != 3) && (!atk.Locked)){

                                            int deploy = 1;
                                            if (biggestenemystack == needed) deploy = 5;
                                            if (armiesLeft > 1)
                                            {
                                                deploy = 1;
                                            }

                                            atk.Armies += deploy;
                                            atk.FromRegion.ReservedArmies += deploy;
                                            armiesLeft -= deploy;

                                            if (atk.Armies >= max) atk.Locked = true;
                                            gotany = true;
                                        }
                                        
                                        if (armiesLeft <= 0) break;

                                        if (!atk.Locked) testLock = false;

                                    }
                                    allLocked = testLock;
                                    
                                }
                                
                            }

                        }

                        //todo: later: if we have multiple areas bordering an enemy, decide in which situations we should attack with all or sit, delay a lot and attack with 2

                    } else {

                        // move leftovers to where they can border an enemy or finish the highest ranked expansion target superregion
                        if (armiesLeft > 0)
                        {
                            bool eborder = false;

                            // go through all the neighbours to see into which is more worth moving into
                            foreach (Region a in fromRegion.Neighbors)
                            {
                                Region an = state.FullMap.GetRegion(a.Id);

                                // check if this neighbour is a neutral being transfered into
                                if (an.OwnedByPlayer("neutral"))
                                {
                                    List<AttackTransferMove> tempATM = new List<AttackTransferMove>();
                                    foreach (AttackTransferMove atm in attackTransferMoves)
                                    {
                                        // if we are already attacking this neutral and have troops to spare
                                        // why not use them to make sure we get the neutral?
                                        if ((armiesLeft > 0) && (atm.FromRegion.Id == fromRegion.Id) && (atm.ToRegion.Id == an.Id))
                                        {
                                            atm.FromRegion.ReservedArmies++;
                                            atm.Armies++;
                                            armiesLeft--;
                                        }

                                        // if it is a neutral and is being attacked from another region, we might as well help out
                                        if ((armiesLeft > 1) && (atm.FromRegion.Id != fromRegion.Id) && (atm.ToRegion.Id == an.Id))
                                        {
                                            tempATM.Add(new AttackTransferMove(myName, fromRegion, an, 2, 10));
                                            armiesLeft -= 2;
                                            fromRegion.ReservedArmies += 2;
                                        }
                                    }

                                    if (tempATM.Count > 0)
                                    {
                                        foreach (AttackTransferMove atm in tempATM) attackTransferMoves.Add(atm);
                                    }
                                }


                                // check if it's not bordering an enemy
                                // if it is, move leftover to where they can border an enemy
                                // or help finish the highest ranked expansion target superregion more easily

                                int count = 0;

                                // neighbour doesnt belong to us, can't move there
                                if (!an.OwnedByPlayer(myName))
                                {
                                    // tag this region as neighboring the enemy
                                    if (an.OwnedByPlayer(opponentName))
                                    {
                                        eborder = true;
                                    }

                                    a.tempSortValue = 0;
                                    continue;
                                }

                                // give bonus depending on it's neighbours
                                if (state.ExpansionTargets.Count > 2)
                                {
                                    foreach (Region neigh in a.Neighbors)
                                    {
                                        Region nee = state.FullMap.GetRegion(neigh.Id);
                                        // facing opponent(s), maximum priority
                                        if (nee.OwnedByPlayer(opponentName)) count += 12;
                                        // bordering another superregion(s)
                                        if (an.SuperRegion.Id != nee.SuperRegion.Id) count += 4;
                                        // meeting up with stack(s) of mine
                                        if (nee.OwnedByPlayer(myName) && (nee.Armies > 6)) count += 3;
                                        // borders neutral(s) from top expansion target (probably itself), might help finish SR faster
                                        if (nee.OwnedByPlayer("neutral") && ((nee.SuperRegion.Id == state.ExpansionTargets[0].Id) || (nee.SuperRegion.Id == state.ExpansionTargets[1].Id))) count+=2;   
                                    }
                                }

                                a.tempSortValue = count;

                            }

                            // process the heuristics
                            var lst = fromRegion.Neighbors.OrderByDescending(p => p.tempSortValue).ToList();
                            if ((armiesLeft > 0) && (lst.Count>0)) { 
                                Region dest = state.FullMap.GetRegion(lst[0].Id);
                                if (dest.OwnedByPlayer(state.MyPlayerName) && (!eborder))
                                {
                                    attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, dest, armiesLeft, 1));
                                    fromRegion.ReservedArmies += armiesLeft;
                                    armiesLeft = 0;
                                }
                            }
                        }
                    }

                }
            }


            // buff up any scheduled attack coming from regions with armies left lying around unused
            // remove any call that might run into a wall (unless it's high priority)
            List<AttackTransferMove> atmRemove = new List<AttackTransferMove>();
            foreach (AttackTransferMove atm in attackTransferMoves)
            {
                Region from = state.FullMap.GetRegion(atm.FromRegion.Id);
                Region to = state.FullMap.GetRegion(atm.ToRegion.Id);
                int armyCount = atm.Armies;

                // if we have orders to in region with some unused troops, use them to buff up the attack
                int armiesAvail = from.Armies + from.PledgedArmies - 1;
                if (armiesAvail > from.ReservedArmies)
                {
                    int narmies = armiesAvail - from.ReservedArmies;
                    from.ReservedArmies += narmies;
                    atm.Armies += narmies;
                    armyCount += narmies;
                }

                // subtract any armies that have already been used up to now
                foreach (AttackTransferMove atmcheck in attackTransferMoves)
                {
                    if (atmcheck == atm) break; // does c# compare successfully 2 objects?

                    Region fromcheck = state.FullMap.GetRegion(atmcheck.FromRegion.Id);

                    if (fromcheck.Id == from.Id)
                    {
                        armyCount -= atmcheck.Armies;
                    }
                }
                //todo: test this


                //todo: remove potential excessive armies used (due to the finish region +1 bug/feature)

                // prevent from hitting a wall against opponent
                if (to.OwnedByPlayer(opponentName))
                {
                    bool attack = true;

                    // if army count is lower then estimated: remove attack from schedule
                    int nstackcount = 0;
                    // check neighbours stack to have better estimate
                    foreach (Region reg in to.Neighbors)
                    {
                        Region regn = state.FullMap.GetRegion(reg.Id);
                        if (regn.OwnedByPlayer(state.OpponentPlayerName))
                        {
                            nstackcount += regn.Armies - 1;
                        }
                    }
                    if (armyCount <= to.Armies + state.EstimatedOpponentIncome + nstackcount) attack = false;
                    
                    // if armies are the same and there was no deployortransfer and it's not just 1 enemyborder: keep attack scheduled
                    if ((armyCount > 1) && (armyCount > to.Armies * 2) && (to.Armies == to.PreviousTurnArmies) && (!to.DeployedOrTransferedThisTurn) && (state.EnemyBorders.Count > 1)) attack = true;

                    // if priority is higher then 8: keep attack scheduled
                    if (atm.Priority >= 8) attack = true;

                    // if armycount is 2: keep attack scheduled
                    if ((armyCount == 2) && (to.Armies == 1)) attack = true;

                    if (!attack)
                    {
                        Console.Error.WriteLine("prevent hitting a wall from " + from.Id + " to " + to.Id + " with " + armyCount + " armies on round " + state.RoundNumber);
                        atmRemove.Add(atm);
                    }
                }
            }
            foreach (AttackTransferMove atm in atmRemove)
            {
                attackTransferMoves.Remove(atm);
            }

            List<AttackTransferMove> sorted = attackTransferMoves.OrderByDescending(p => p.Armies).OrderBy(p => p.Priority).ToList();
           
            return sorted;
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
                Console.Error.WriteLine("running null parser due to " + e.ToString());
                parser.Run(null);
            }
        }

    }
    
}