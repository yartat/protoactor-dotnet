using System;
using System.Runtime.Serialization;

namespace Node2.Contracts
{
    /// <summary>
    /// Base transaction request class
    /// </summary>
    [DataContract]
    public class BaseTransaction
    {
        /// <summary>
        /// Gets or sets the transaction identifier.
        /// </summary>
        /// <value>
        /// The transaction identifier.
        /// </value>
        [DataMember(Name = "id")]
        public string TransactionId { get; set; }

        /// <summary>
        /// Gets or sets the transaction creation date and time.
        /// </summary>
        /// <value>
        /// The transaction creation date and time.
        /// </value>
        [DataMember(Name = "date")]
        public DateTimeOffset CreatedOn { get; set; }

        /// <summary>
        /// Gets or sets the transaction player identifier.
        /// </summary>
        /// <value>
        /// The transaction player identifier.
        /// </value>
        [DataMember(Name = "playerId")]
        public string PlayerId { get; set; }
    }
}