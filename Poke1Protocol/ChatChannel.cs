using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Poke1Protocol
{
    public class ChatChannel
    {
        public string Id { get; private set; }
        public string Name { get; private set; }

        public ChatChannel(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }
}
