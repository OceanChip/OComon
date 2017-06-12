using OceanChip.Common.Remoting;
using OceanChip.Common.Socketing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace OceanChip.Common.Extensions
{
    public static class RemotingClientExtensions
    {
        public static IEnumerable<SocketRemotingClient> ToRomotingClientList(this IEnumerable<IPEndPoint> endpointList,SocketSetting setting)
        {
            var remotingClientList = new List<SocketRemotingClient>();

            foreach(var endpoint in endpointList)
            {
                var remotingClient = new SocketRemotingClient(endpoint, setting);
                remotingClientList.Add(remotingClient);
            }
            return remotingClientList;
        }
    }
}
