using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Opc.Ua;

namespace OPCUAClientDynamicTypesSample
{
    public interface IConnector
    {
        Task<Dictionary<ExpandedNodeId, Type>> ConnectAndCreateDynamicTypes();
    }
}
