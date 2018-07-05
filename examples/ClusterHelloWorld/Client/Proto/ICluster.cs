using System;
using System.Threading.Tasks;
using Messages;

namespace Client.Proto
{
    /// <summary>
    /// 
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public interface ICluster : IDisposable
    {
        /// <summary>
        /// Makes the deposit.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="type">The type.</param>
        /// <param name="request">The request.</param>
        /// <returns></returns>
        Task<DepositResponse> MakeDeposit(string id, string type, DepositRequest request);
    }
}