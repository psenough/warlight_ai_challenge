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

            var finishableSuperRegion = state.ExpansionTargets[0].IsFinishable(state.StartingArmies);

            //todo: check with rules if player1 is always us and player2 always enemy, or if it should be extracted somehow
            var enemySighted = false;
            foreach (Region reg in state.VisibleMap.regions)
            {
                if (reg.OwnedByPlayer("player2")) {
                    enemySighted = true;
                    break;
                }
            }

            List<PlaceArmiesMove> placeArmiesMoves = new List<PlaceArmiesMove>();
            String myName = state.MyPlayerName;
            int armiesLeft = state.StartingArmies;

            if (finishableSuperRegion)
            {
                //todo: later: calculate if we should be attacking a particular region strongly (in case there is chance of counter or defensive positioning)

                // if you have enough income to finish an area this turn, deploy for it
                foreach (Region reg in state.ExpansionTargets[0].SubRegions)
                {
                    // find the player1 neighbour with highest available armies
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
                //todo: finish this sorter function
                Region target = state.ExpansionTargets[0].SubRegions[0].Neighbors[0];
                Region attacker = state.ExpansionTargets[0].SubRegions[0];
                int deployed = state.ScheduleNeutralAttack( target, attacker, armiesLeft);
                placeArmiesMoves.Add(new PlaceArmiesMove(myName, attacker, deployed));
                armiesLeft -= deployed;


                //todo: find all regions where we border enemy
                //todo: deploy rest of your income bordering the enemy
                
                //todo: later: decide if we should deploy all in one place and attack hard or not

            }
            else
            {
                //todo: deploy all on the two main expansion targets
                //todo: need to decide if should be done with stack or scatter, depending on how likely enemy is close

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
            //todo: for each region, process scheduledattacks

            List<AttackTransferMove> attackTransferMoves = new List<AttackTransferMove>();
            String myName = state.MyPlayerName;
            int armies = 5;

            foreach (var fromRegion in state.VisibleMap.Regions)
            {
                if (fromRegion.OwnedByPlayer(myName)) //do an attack
                {
                    List<Region> possibleToRegions = new List<Region>();
                    possibleToRegions.AddRange(fromRegion.Neighbors);

                    while (possibleToRegions.Any())
                    {
                        double rand = Random.NextDouble();
                        int r = (int)(rand * possibleToRegions.Count);
                        Region toRegion = possibleToRegions[r];

                        if (toRegion.PlayerName != myName && fromRegion.Armies > 6) //do an attack
                        {
                            attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, toRegion, armies));
                            break;
                        }
                        else if (toRegion.PlayerName == myName && fromRegion.Armies > 1) //do a transfer
                        {
                            attackTransferMoves.Add(new AttackTransferMove(myName, fromRegion, toRegion, armies));
                            break;
                        }
                        else
                            possibleToRegions.Remove(toRegion);
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