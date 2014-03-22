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

        List<PlaceArmiesMove> GetPlaceArmiesMoves(BotState state, long timeOut);

        List<AttackTransferMove> GetAttackTransferMoves(BotState state, long timeOut);

    }

}