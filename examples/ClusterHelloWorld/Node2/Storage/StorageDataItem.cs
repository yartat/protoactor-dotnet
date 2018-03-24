using System.Runtime.Serialization;

namespace Node2.Storage
{
    [DataContract]
    public class StorageDataItem
    {
        [DataMember(Name = "value")]
        public string Value { get; set; }
    }
}