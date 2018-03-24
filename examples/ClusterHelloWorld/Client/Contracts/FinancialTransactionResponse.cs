using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Client.Contracts
{
    /// <summary>
    /// Describes transaction response contract
    /// </summary>
    [DataContract]
    public class FinancialTransactionResponse
    {
        /// <summary>
        /// Gets or sets unique identifier of the transaction performed. Uniqueness should be guaranteed within the same transaction type.
        /// </summary>
        /// <value>
        /// Unique identifier of the transaction performed.
        /// </value>
        [DataMember(Name = "Id")]
        public string TransactionId { get; set; }

        /// <summary>
        /// Specifies whether this transaction was actually processed as a result of the request.
        /// </summary>
        /// <value>
        /// Is transaction has been already processed.
        /// </value>
        [DataMember(Name = "alreadyProcessed", EmitDefaultValue = false)]
        public bool? AlreadyProcessed { get; set; }

        /// <summary>
        /// Gets or sets the time when the transaction was processed.
        /// </summary>
        /// <value>
        /// The transaction processed time.
        /// </value>
        [DataMember(Name = "processedTime")]
        public DateTimeOffset ProcessedTime { get; set; }

        /// <summary> 
        /// Gets or sets the balances after processing transaction. 
        /// </summary>
        /// <value>
        /// The balances after processing transaction.
        /// </value>
        [DataMember(Name = "balances")]
        public Dictionary<string, decimal> Balances { get; set; }
    }
}