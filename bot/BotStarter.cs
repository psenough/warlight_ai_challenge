using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using main;
using move;

namespace bot
{

    /**
     * This is a simple bot that does random (but correct) moves.
     * This class implements the Bot interface and overrides its Move methods.
     * You can implements these methods yourself very easily now,
     * since you can retrieve all information about the match from variable “state”.
     * When the bot decided on the move to make, it returns a List of Moves. 
     * The bot is started by creating a Parser to which you Add
     * a new instance of your bot, and then the parser is started.
     */

    public class BotStarter : Bot
    {
        public static Random Random = new Random();

        /**
         * A method used at the start of the game to decide which player start with what Regions. 6 Regions are required to be returned.
         * This example randomly picks 6 regions from the pickable starting Regions given by the engine.
         * @return : a list of m (m=6) Regions starting with the most preferred Region and ending with the least preferred Region to start with 
         */
        public List<Region> GetPreferredStartingRegions(BotState state, long timeOut)
        {
            state.FullMap.SuperRegions.Sort(new SuperRegionsLowerArmiesSorter());
            state.PickableStartingRegions.Sort(new RegionsImportanceSorter(state.FullMap.SuperRegions));
            //state.PickableStartingRegions.RemoveRange(6, state.PickableStartingRegions.Length-6);

            // assume opponent will also choose optimum picks
            // this will be useful later when we need to predict where opponent started
            state.OpponentStartRegions = state.PickableStartingRegions;

            return state.PickableStartingRegions;
        }

        public List<PlaceArmiesMove> DeployAtRandom(BotState state, int armiesLeft) {
            List<PlaceArmiesMove> placeArmiesMoves = new List<PlaceArmiesMove>();
            String myName = state.MyPlayerName;
            int armies = 2;
            var visibleRegions = state.VisibleMap.Regions;

            while (armiesLeft > 0)
            {
                double rand = Random.NextDouble();
                int r = (int)(rand * visibleRegions.Count);
                var region = visibleRegions.ElementAt(r);

                if (region.OwnedByPlayer(myName))
                {
                    placeArmiesMoves.Add(new PlaceArmiesMove(myName, region, armies));
                    armiesLeft -= armies;
                }
            }
            return placeArmiesMoves;
        }

        /**
         * This method is called for at first part of each round. This example puts two armies on random regions
         * until he has no more armies left to place.
         * @return The list of PlaceArmiesMoves for one round
         */
        public List<PlaceArmiesMove> GetPlaceArmiesMoves(BotState state, long timeOut)
        {

            String myName = state.MyPlayerName;

            var finishableSuperRegion = state.ExpansionTargets[0].IsFinishable(state.StartingArmies, myName);

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
                    // find our neighbour with highest available armies
                    reg.Neighbors.Sort(new RegionsAvailableArmiesSorter());

                    if (reg.OwnedByPlayer("neutral"))
                    {
                        int deployed = state.ScheduleNeutralAttack(reg, reg.Neighbors[0], armiesLeft);
                        placeArmiesMoves.Add(new PlaceArmiesMove(myName, reg, deployed));
                        armiesLeft -= deployed;
                    }
                }

                if (armiesLeft < 0) Console.Error.WriteLine("exceeded army deployment!");
            
                // deploy the rest of our armies randomly
                if (armiesLeft > 0)
                {
                    List<PlaceArmiesMove> placings = DeployAtRandom(state, armiesLeft);
                    foreach (PlaceArmiesMove pl in placings)
                    {
                        placeArmiesMoves.Add(pl);
                    }
                }

            }
            else if (enemySighted)
            {
                //todo: later: dont bother expanding on areas that might have enemy in a few turns

                // do minimum expansion on our best found expansion target
                state.ExpansionTargets[0].SubRegions.Sort(new RegionsMinimumExpansionSorter());
                state.ExpansionTargets[0].SubRegions[0].Neighbors.Sort(new RegionsBestExpansionNeighborSorter());
                Region target = state.ExpansionTargets[0].SubRegions[0].Neighbors[0];
                Region attacker = state.ExpansionTargets[0].SubRegions[0];
                int deployed = state.ScheduleNeutralAttack( target, attacker, armiesLeft);
                placeArmiesMoves.Add(new PlaceArmiesMove(myName, attacker, deployed));
                armiesLeft -= deployed;

                // deploy rest of your income bordering the enemy
                while (armiesLeft > 0)
                {
                    foreach (Region reg in enemyBorders)
                    {
                        placeArmiesMoves.Add(new PlaceArmiesMove(myName, reg, armiesLeft--));
                        if (armiesLeft == 0) break;
                    }
                }
                
                //todo: later: decide if we should deploy all in one place and attack hard, or spread out the deployments and sit

            }
            else
            {
                // deploy all on the two main expansion targets
                List<Region> deployingPlaces = new List<Region>();
                
                foreach (Region reg in state.ExpansionTargets[0].SubRegions)
                {
                    if (reg.OwnedByPlayer(myName)) deployingPlaces.Add(reg);
                }
                foreach (Region reg in state.ExpansionTargets[1].SubRegions)
                {
                    if (reg.OwnedByPlayer(myName)) deployingPlaces.Add(reg);
                }
                while (armiesLeft > 0)
                {
                    foreach (Region reg in deployingPlaces)
                    {
                        placeArmiesMoves.Add(new PlaceArmiesMove(myName, reg, armiesLeft--));
                        if (armiesLeft == 0) break;
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

            foreach (Region fromRegion in state.VisibleMap.Regions)
            {
                if (fromRegion.OwnedByPlayer(myName))
                {
                    // process already scheduled attacks (during the place armies phase)
                    if (fromRegion.scheduledAttack.Count > 0)
                    {
                        foreach (Tuple<Region, int> tup in fromRegion.scheduledAttack)
                        {
                            attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, tup.Item1, tup.Item2));
                        }
                    }

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
                            attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, fromRegion.Neighbors[0], armiesLeft));
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