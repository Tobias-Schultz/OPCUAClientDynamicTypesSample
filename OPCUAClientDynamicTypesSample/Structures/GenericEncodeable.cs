using System;
using Opc.Ua;

namespace OPCUAClientDynamicTypesSample.Structures
{
    public abstract class GenericEncodeable: IEncodeable
    {
        /// <summary>
        /// Encodes the object with the specified encoder.
        /// </summary>
        /// <param name="encoder">The encoder.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public void Encode(IEncoder encoder)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Decodes the object with the specified decoder.
        /// </summary>
        /// <param name="decoder">The decoder.</param>
        public void Decode(IDecoder decoder)
        {
            if (decoder != null)
            {
                var properties = GetType().GetProperties();
                foreach (var property in properties)
                {
                    if (property.Name.Equals(nameof(NodeId)) || property.Name.Equals(nameof(Namespace)) || property.Name.Equals(nameof(TypeId)) || property.Name.Equals(nameof(BinaryEncodingId)) || property.Name.Equals(nameof(XmlEncodingId)))
                    {
                        continue;
                    }

                    if (property.PropertyType == typeof(string))
                    {
                        var value = decoder.ReadString(property.Name);
                        property.SetValue(this, value);
                    }
                    else if (property.PropertyType == typeof(bool))
                    {
                        var value = decoder.ReadBoolean(property.Name);
                        property.SetValue(this, value);
                    }
                    else if (property.PropertyType == typeof(DateTime))
                    {
                        var value = decoder.ReadDateTime(property.Name);
                        property.SetValue(this, value);
                    }
                    else if (property.PropertyType == typeof(LocalizedText))
                    {
                        property.SetValue(this, decoder.ReadLocalizedText(property.Name));
                    }
                    else if (property.PropertyType == typeof(StringCollection))
                    {
                        property.SetValue(this, decoder.ReadStringArray(property.Name));
                    }
                    else if (property.PropertyType == typeof(uint))
                    {
                        property.SetValue(this, decoder.ReadUInt32(property.Name));
                    }
                    else if (property.PropertyType == typeof(int))
                    {
                        var value = decoder.ReadInt32(property.Name);
                        property.SetValue(this, value);
                    }
                    else
                    {
                        var array = decoder.ReadEnumeratedArray(property.Name, property.PropertyType.GetElementType());
                        property.SetValue(this, array);
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the specified encodeable is equal.
        /// </summary>
        /// <param name="encodeable">The encodeable.</param>
        /// <returns>
        ///   <c>true</c> if the specified encodeable is equal; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.NotImplementedException"></exception>
        public bool IsEqual(IEncodeable encodeable)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// The NodeId for the Encodeable.
        /// </summary>
        protected abstract uint NodeId { get; }
        /// <summary>
        /// The Namespace for the Encodeable.
        /// </summary>
        protected abstract string Namespace { get; }
        /// <summary>
        ///  The TypeId for Encodeable.
        /// </summary>
        public ExpandedNodeId TypeId => new ExpandedNodeId(NodeId, Namespace);
        /// <summary>
        ///  The BinaryEncodingId for the Encodeable.
        /// </summary>
        public ExpandedNodeId BinaryEncodingId => new ExpandedNodeId(NodeId, Namespace);
        /// <summary>
        ///  The XmlEncodingId for the Encodeable.
        /// </summary>
        public ExpandedNodeId XmlEncodingId => new ExpandedNodeId(NodeId, Namespace);
    }
}
