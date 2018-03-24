using System.Runtime.Serialization;

namespace Node2.Contracts
{
    /// <summary>
    /// Deposit transaction request
    /// </summary>
    /// <seealso cref="FinancialTransaction" />
    [DataContract]
    public class DepositTransaction : FinancialTransaction
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
        public bool Manual { get; set; }
    }
}