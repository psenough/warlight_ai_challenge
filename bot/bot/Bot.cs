using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using main;
using move;


namespace bot
{

    public interface Bot
    {

        List<Region> GetPreferredStartingRegions(BotState state, long timeOut);

        List<DeployArmies> DeployBorderingEnemy(BotState state, int armiesLeft);

        List<DeployArmies> DeployAtRandom(List<Region> list, BotState state, string myName, int armiesLeft);

        List<DeployArmies> FinishSuperRegion(BotState state, int armiesLeft);

        List<DeployArmies> ExpandNormal(BotState state, int armiesLeft);

        List<DeployArmies> ExpandMinimum(BotState state, int armiesLeft);

        List<DeployArmies> AttackHard(BotState state, int armiesLeft);

        List<DeployArmies> GetDeployArmiesMoves(BotState state, long timeOut);

        List<AttackTransferMove> GetAttackTransferMoves(BotState state, long timeOut);

    }

}