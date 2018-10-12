using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using OPCUAClientDynamicTypesSample.Structures;

namespace OPCUAClientDynamicTypesSample
{
    public class Connector : IConnector
    {
        private EndpointConfiguration _endpointConfig;
        private ApplicationConfiguration _appConfig;
        private Dictionary<string, AssemblyBuilder> _dynamicAssemblies;
        private Dictionary<ExpandedNodeId, Type> _createdTypeDictionary;
        private Session _session;

        // Settings
        private const string OpcEndpointAddress = "opc.tcp://localhost:51210/UA/SampleServer";
        private const string SessionName = "DynamicTypesSession";
        private const string ApplicationName = "OPCUAClient DynamicTypesSample";
        private const string ConfigSectionName = "Opc.Ua.DynamicTypesSample";

        private const string DynamicOpcTypesString = "DynamicOPCTypes";
        private const string DynamicOpcModuleString = "DynamicOpcModule";
        private const string SetMethodPreFixString = "set_";
        private const string GetMethodPreFixString = "get_";
        private const string EnumeratedTypeString = "EnumeratedType";
        private const string NameString = "Name";
        private const string EnumeratedValueString = "EnumeratedValue";
        private const string ValueString = "Value";
        private const string StructuredTypeString = "StructuredType";
        private const string NodeIdString = "NodeId";
        private const string NamespaceString = "Namespace";
        private const string FieldString = "Field";
        private const string TypeNameString = "TypeName";
        private const string NoOfString = "NoOf";
        private const string OpcStringString = "opc:String";
        private const string UaLocalizedTextString = "ua:LocalizedText";
        private const string OpcBooleanString = "opc:Boolean";
        private const string OpcDateTimeString = "opc:DateTime";
        private const string OpcInt32String = "opc:Int32";
        private const string OpcUint32String = "opc:UInt32";
        private const string TnsPrefixString = "tns:";
        private const string LengthFieldString = "LengthField";


        public async Task<Dictionary<ExpandedNodeId,Type>> ConnectAndCreateDynamicTypes()
        {
            _session = await OpenSession();
            return CreateDynamicTypes();
        }

        private static void CreateDynamicEnum(EnumBuilder enumBuilder, string typeName, string typeValue)
        {
            enumBuilder.DefineLiteral(typeName, int.Parse(typeValue));
        }

        private static void BuildDynamicProperty(TypeBuilder typeBuilder, string propertyName, Type propertyType, object propertyValue, bool createWithField = false)
        {
            var field = typeBuilder.DefineField("_" + propertyName, propertyType, FieldAttributes.Private | FieldAttributes.Static);
            var propertyBuilder = typeBuilder.DefineProperty(propertyName, PropertyAttributes.None, propertyType, null);
            var methodAttributes = System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.HideBySig | System.Reflection.MethodAttributes.Virtual;

            if (createWithField)
            {
                var setter = typeBuilder.DefineMethod(SetMethodPreFixString + propertyName, methodAttributes, null, new[] { propertyType });
                var setIl = setter.GetILGenerator();
                setIl.Emit(OpCodes.Ldarg_0);
                setIl.Emit(OpCodes.Ldarg_1);
                setIl.Emit(OpCodes.Stfld, field);
                setIl.Emit(OpCodes.Ret);

                var getter = typeBuilder.DefineMethod(GetMethodPreFixString + propertyName, methodAttributes, propertyType, Type.EmptyTypes);
                var getIl = getter.GetILGenerator();
                getIl.Emit(OpCodes.Ldarg_0);
                getIl.Emit(OpCodes.Ldfld, field);
                getIl.Emit(OpCodes.Ret);

                propertyBuilder.SetGetMethod(getter);
                propertyBuilder.SetSetMethod(setter);

                var customAtributeType = typeof(System.Runtime.CompilerServices.RuntimeCompatibilityAttribute);
                var customAttributeBuilder = new CustomAttributeBuilder(
                    customAtributeType.GetConstructor(Type.EmptyTypes), new object[] { });

                propertyBuilder.SetCustomAttribute(customAttributeBuilder);
            }
            else
            {
                var getter = typeBuilder.DefineMethod(GetMethodPreFixString + propertyName, methodAttributes, propertyType, Type.EmptyTypes);

                var getIl = getter.GetILGenerator();
                switch (propertyValue)
                {
                    case uint uvalue:
                        getIl.Emit(OpCodes.Ldc_I4, uvalue);
                        getIl.Emit(OpCodes.Ret);
                        break;
                    case string svalue:
                        getIl.Emit(OpCodes.Ldstr, svalue);
                        getIl.Emit(OpCodes.Ret);
                        break;
                }
                propertyBuilder.SetGetMethod(getter);
            }
        }

        private Dictionary<ExpandedNodeId, Type> CreateDynamicTypes()
        {
            // Find custom enums and types on the opc ua server which is mainly used to retrieve the encoding id for the type creation.
            var foundCustomTypes = new Dictionary<QualifiedName, NodeId>();
            FindEncodedEnumsIds(DataTypeIds.Enumeration, ref foundCustomTypes);
            FindEncodedTypeIds(ObjectIds.OPCBinarySchema_TypeSystem, ref foundCustomTypes);
            if (!foundCustomTypes.Any())
            {
                return new Dictionary<ExpandedNodeId, Type>();
            }

            // Create (if necessary) the dynamic assembly where the dynamic module will be added.
            if (_dynamicAssemblies == null)
            {
                _dynamicAssemblies = new Dictionary<string, AssemblyBuilder>();
                _createdTypeDictionary = new Dictionary<ExpandedNodeId, Type>();
            }
            AssemblyBuilder assemblymBuilder;
            if (_dynamicAssemblies.ContainsKey(DynamicOpcTypesString))
            {
                assemblymBuilder = _dynamicAssemblies[DynamicOpcTypesString];
            }
            else
            {
                assemblymBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(Guid.NewGuid().ToString()), AssemblyBuilderAccess.Run);
                _dynamicAssemblies.Add(DynamicOpcTypesString, assemblymBuilder);
            }
            // Create (if necessary) the dynamic module where the types will be added.
            var moduleBuilder = assemblymBuilder.GetDynamicModule(DynamicOpcModuleString);
            if (moduleBuilder == null)
            {
                moduleBuilder = assemblymBuilder.DefineDynamicModule(DynamicOpcModuleString);
            }

            // Fetch the description of the custom namespaces via the OPCBinarySchema_TypeSystem Node and decode it.
            var binaryTypeData = new List<DataValue>();
            var stringTypeData = new List<string>();
            var binaryTypeNode = _session.ReadNode(ObjectIds.OPCBinarySchema_TypeSystem);
            var referenceTypeNodes = _session.FetchReferences(binaryTypeNode.NodeId).Where(d => d.IsForward && d.NodeClass == NodeClass.Variable);
            foreach (var referenceDescription in referenceTypeNodes)
            {
                binaryTypeData.Add(_session.ReadValue(ExpandedNodeId.ToNodeId(referenceDescription.NodeId, _session.NamespaceUris)));
            }
            foreach (var byteValue in binaryTypeData)
            {
                stringTypeData.Add(Encoding.ASCII.GetString((byte[])byteValue.Value));
            }

            // Parse the decoded namespace descriptions and create types on base of the informations and the encoded node ids.
            // Skip default opc namespace.
            for (var i = 1; i < stringTypeData.Count; i++)
            {
                var typeData = stringTypeData[i];
                var xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(typeData);

                // Create enums (which might be used for the types later)
                foreach (XmlNode child in xmlDocument.ChildNodes)
                {
                    if (child.HasChildNodes)
                    {
                        foreach (XmlNode subchild in child.ChildNodes)
                        {
                            if (subchild.LocalName.Equals(EnumeratedTypeString))
                            {
                                var name = new QualifiedName(subchild.Attributes[NameString]?.Value, 3);
                                if (foundCustomTypes.ContainsKey(name))
                                {
                                    var encodingId = foundCustomTypes[name];
                                    var enumBuilder = moduleBuilder.DefineEnum(name.Name, TypeAttributes.Public, typeof(int));
                                    if (subchild.HasChildNodes)
                                    {
                                        foreach (XmlNode subsubChild in subchild.ChildNodes)
                                        {
                                            if (subsubChild.LocalName.Equals(EnumeratedValueString))
                                            {
                                                var typeName = subsubChild.Attributes[NameString]?.Value;
                                                var typeValue = subsubChild.Attributes[ValueString].Value;
                                                CreateDynamicEnum(enumBuilder, typeName, typeValue);
                                            }
                                        }
                                    }
                                    var newType = enumBuilder.CreateTypeInfo();
                                    _createdTypeDictionary.Add(encodingId, newType);
                                }
                            }
                        }
                    }
                }

                // Create types
                foreach (XmlNode child in xmlDocument.ChildNodes)
                {
                    if (child.HasChildNodes)
                    {
                        foreach (XmlNode subchild in child.ChildNodes)
                        {
                            if (subchild.LocalName.Equals(StructuredTypeString))
                            {
                                var name = new QualifiedName(subchild.Attributes[NameString]?.Value, 3);
                                if (foundCustomTypes.ContainsKey(name))
                                {
                                    var encodingId = foundCustomTypes[name];
                                    var typeBuilder = moduleBuilder.DefineType(name.Name, TypeAttributes.Public | TypeAttributes.Class, typeof(GenericEncodeable));
                                    typeBuilder.AddInterfaceImplementation(typeof(IEncodeable));
                                    BuildDynamicProperty(typeBuilder, NodeIdString, typeof(uint), (uint)encodingId.Identifier);
                                    BuildDynamicProperty(typeBuilder, NamespaceString, typeof(string), encodingId.NamespaceIndex.ToString());
                                    if (subchild.HasChildNodes)
                                    {
                                        foreach (XmlNode subsubChild in subchild.ChildNodes)
                                        {
                                            if (subsubChild.LocalName.Equals(FieldString))
                                            {
                                                var typeName = subsubChild.Attributes[NameString]?.Value;
                                                var typeType = subsubChild.Attributes[TypeNameString].Value;
                                                if (typeName == null || typeType == null)
                                                {
                                                    continue;
                                                }
                                                Type propertyType = null;
                                                if (typeName.Contains(NoOfString))
                                                {
                                                    continue;
                                                }
                                                if (typeType.Equals(OpcStringString))
                                                {
                                                    propertyType = typeof(string);
                                                }
                                                else if (typeType.Equals(OpcBooleanString))
                                                {
                                                    propertyType = typeof(bool);
                                                }
                                                else if (typeType.Equals(UaLocalizedTextString))
                                                {
                                                    propertyType = typeof(LocalizedText);
                                                }
                                                else if (typeType.Equals(OpcUint32String))
                                                {
                                                    propertyType = typeof(uint);
                                                }
                                                else if (typeType.Equals(OpcDateTimeString))
                                                {
                                                    propertyType = typeof(DateTime);
                                                }
                                                else if (typeType.Equals(OpcInt32String))
                                                {
                                                    propertyType = typeof(int);
                                                }
                                                else if (typeType.StartsWith(TnsPrefixString))
                                                {
                                                    var knownType = _createdTypeDictionary.Values.FirstOrDefault(t =>
                                                        t.Name.Equals(typeType.Replace(TnsPrefixString, string.Empty)));
                                                    propertyType = knownType;
                                                    if (subsubChild.Attributes[LengthFieldString]?.Value != null)
                                                    {
                                                        propertyType = knownType?.MakeArrayType();
                                                    }
                                                }
                                                if (propertyType != null)
                                                {
                                                    BuildDynamicProperty(typeBuilder, typeName, propertyType, string.Empty, true);
                                                }
                                            }
                                        }
                                    }
                                    var createdType = typeBuilder.CreateType();
                                    _createdTypeDictionary.Add(encodingId, createdType);
                                    _session.Factory.AddEncodeableType(encodingId, createdType);
                                }
                            }
                        }
                    }
                }
            }
            return _createdTypeDictionary;
        }

        private void FindEncodedTypeIds(NodeId root, ref Dictionary<QualifiedName, NodeId> dynamicTypes, int layer = 0)
        {
            if (dynamicTypes == null)
            {
                return;
            }

            BrowseDirection direction;
            NodeId referenceTypeId;
            switch (layer)
            {
                case 0:
                    direction = BrowseDirection.Forward;
                    referenceTypeId = ReferenceTypeIds.HasComponent;
                    break;
                case 1:
                    direction = BrowseDirection.Forward;
                    referenceTypeId = ReferenceTypeIds.HasComponent;
                    break;
                case 2:
                    direction = BrowseDirection.Inverse;
                    referenceTypeId = ReferenceTypeIds.HasDescription;
                    break;
                case 3:
                    direction = BrowseDirection.Inverse;
                    referenceTypeId = ReferenceTypeIds.HasEncoding;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _session.Browse(
                null,
                null,
                root,
                0u,
                direction,
                referenceTypeId,
                true,
                (uint)NodeClass.Unspecified,
                out _,
                out var references);

            foreach (var reference in references)
            {
                var nodeId = ExpandedNodeId.ToNodeId(reference.NodeId, _session.NamespaceUris);
                if (layer < 3)
                {
                    FindEncodedTypeIds(nodeId, ref dynamicTypes, layer + 1);
                }
                else
                {
                    if (!dynamicTypes.ContainsKey(reference.BrowseName.Name))
                    {
                        dynamicTypes.Add(reference.BrowseName, nodeId);
                    }
                }
            }



        }

        private void FindEncodedEnumsIds(NodeId root, ref Dictionary<QualifiedName, NodeId> dynamicTypes)
        {
            if (dynamicTypes == null)
            {
                return;
            }
            _session.Browse(
                null,
                null,
                root,
                0u,
                BrowseDirection.Forward,
                ReferenceTypeIds.HasSubtype,
                true,
                (uint)NodeClass.Unspecified,
                out _,
                out var references);
            if (references == null)
            {
                return;
            }
            foreach (var reference in references)
            {
                // Skip default opc namespace
                if (reference.BrowseName.NamespaceIndex > 0)
                {
                    if (!dynamicTypes.ContainsKey(reference.BrowseName))
                    {
                        dynamicTypes.Add(reference.BrowseName,
                            ExpandedNodeId.ToNodeId(reference.NodeId, _session.NamespaceUris));
                    }
                }
            }
        }

        private async Task<Session> OpenSession()
        {
            var application = new ApplicationInstance
            {
                ApplicationName = ApplicationName,
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = ConfigSectionName
            };

            _appConfig = await application.LoadApplicationConfiguration(false);
            var appCertificate = _appConfig.SecurityConfiguration.ApplicationCertificate.Certificate;
            if (appCertificate == null)
            {
                appCertificate = CertificateFactory.CreateCertificate(
                    _appConfig.SecurityConfiguration.ApplicationCertificate.StoreType,
                    _appConfig.SecurityConfiguration.ApplicationCertificate.StorePath,
                    null,
                    _appConfig.ApplicationUri,
                    _appConfig.ApplicationName,
                    _appConfig.SecurityConfiguration.ApplicationCertificate.SubjectName,
                    null,
                    CertificateFactory.defaultKeySize,
                    DateTime.UtcNow - TimeSpan.FromDays(1),
                    CertificateFactory.defaultLifeTime,
                    CertificateFactory.defaultHashSize);
                _appConfig.SecurityConfiguration.ApplicationCertificate.Certificate = appCertificate;
            }
            _appConfig.ApplicationUri = Utils.GetApplicationUriFromCertificate(_appConfig.SecurityConfiguration.ApplicationCertificate.Certificate);
            _appConfig.CertificateValidator.CertificateValidation += OnCertificateValidation;

            _endpointConfig = EndpointConfiguration.Create(_appConfig);
            var endPoint = CoreClientUtils.SelectEndpoint(OpcEndpointAddress, false, 15000);

            var identity = new UserIdentity();
            var endpoint = new ConfiguredEndpoint(null, endPoint, _endpointConfig);

            return await Session.Create(_appConfig, endpoint, false, SessionName, 10000, identity, null);
        }

        private static void OnCertificateValidation(CertificateValidator sender, CertificateValidationEventArgs e)
        {
            throw new NotImplementedException();
        }

    }
}
