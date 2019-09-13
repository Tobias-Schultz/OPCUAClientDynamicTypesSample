using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Text;

namespace OPCUAClientDynamicTypesSample
{
    public class BinaryEncodingInformation
    {
        public NodeId NodeId {get;set;}
        public string TypeDictionaryXml { get; set; }

        public QualifiedName Name { get; set; }
    }
}
