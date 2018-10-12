using System;

namespace OPCUAClientDynamicTypesSample
{
    static class Program
    {
        static void Main(string[] args)
        {
            IConnector connector = new Connector();
            var task = connector.ConnectAndCreateDynamicTypes();
            if (task.Result != null)
            {
                foreach (var createdDynamicType in task.Result)
                {
                    Console.WriteLine($"Created dynamic type \"{createdDynamicType.Value}\" with node id \"{createdDynamicType.Key}\". ");
                }
            }
            Console.ReadKey();
        }

    }
}
