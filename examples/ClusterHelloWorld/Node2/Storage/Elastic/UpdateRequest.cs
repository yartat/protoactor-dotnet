using System.Runtime.Serialization;

namespace Node2.Storage.Elastic
{
    [DataContract]
    public class UpdateRequest<TDocument> where TDocument : class
    {
        [DataMember(Name = "doc")]
        public TDocument Document { get; set; }

        [DataMember(Name = "doc_as_upsert")]
        public bool Upsert { get; set; }
    }
}