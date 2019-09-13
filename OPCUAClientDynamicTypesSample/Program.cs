using Opc.Ua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml;

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
                    Console.WriteLine($"--------------------------------------------------------------------------------------------");
                    Console.WriteLine($"Created dynamic type \"{createdDynamicType.Value}\" with node id \"{createdDynamicType.Key}\". ");

                    if (createdDynamicType.Value.IsEnum)
                    {
                        Console.WriteLine("Enum Values:");

                        foreach(var enumValue in Enum.GetValues(createdDynamicType.Value))
                        {
                            Console.WriteLine($"{(int)enumValue} = {enumValue.ToString()}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("StructuredType Properties:");

                        foreach (var prop in createdDynamicType.Value.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
                        {
                            if (prop.Name == "NodeId" || prop.Name == "Namespace")
                                continue;

                            Console.WriteLine($"{prop.PropertyType.Name} {prop.Name}");
                        }

                        Console.WriteLine("StructuredType Methods:");

                        foreach (var function in createdDynamicType.Value.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public))
                        {
                            if (function.Name.StartsWith("get_") || function.Name.StartsWith("set_"))
                            {
                                //ignore getter and setter
                                continue;
                            }

                            string strParameters = string.Join(",", function.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));

                            Console.WriteLine($"{function.ReturnParameter.Name} {function.Name}({strParameters})");
                        }
                    }
                }
            }
            Console.ReadKey();
        }

    }
}
