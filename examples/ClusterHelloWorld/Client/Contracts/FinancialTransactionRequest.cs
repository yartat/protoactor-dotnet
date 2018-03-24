using System.Runtime.Serialization;

namespace Client.Contracts
{
    /// <summary>
    /// Financial transaction request class
    /// </summary>
    [DataContract]
    public class FinancialTransactionRequest : BaseTransactionRequest
    {

        /// <summary>
        /// Gets or sets the transaction amount.
        /// </summary>
        /// <value>
        /// The transaction amount.
        /// </value>
        [DataMember(Name = "amount")]
        public decimal? Amount { get; set; }

        /// <summary>
        /// Gets or sets the currency code.
        /// </summary>
        /// <value>
        /// The currency code.
        /// </value>
        [DataMember(Name = "currency")]
        public string CurrencyCode { get; set; }
    }
}