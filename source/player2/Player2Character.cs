using System.Collections.Generic;

namespace EchoColony
{
    public class Player2Character
    {
        public string id;
        public string short_name;
        public string description;
        public string voice_id;
    }

    public static class Player2CharacterCache
    {
        public static List<Player2Character> Characters = new List<Player2Character>();
    }
}
