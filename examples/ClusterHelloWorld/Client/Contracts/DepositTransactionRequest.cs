using System.Runtime.Serialization;

namespace Client.Contracts
{
    /// <summary>
    /// Deposit transaction request
    /// </summary>
    /// <seealso cref="FinancialTransactionRequest" />
    [DataContract]
    public class DepositTransactionRequest : FinancialTransactionRequest
    {
        /// <summary>
        /// Gets or sets the kiosk identifier.
        /// </summary>
        /// <value>
        /// The kiosk identifier.
        /// </value>
        [DataMember(Name = "kiosk")]
        public string KioskId { get; set; }

        /// <summary>
        /// Gets or sets is deposit manual or not.
        /// </summary>
        [DataMember(Name = "manual")]
        public bool? Manual { get; set; }
    }
}