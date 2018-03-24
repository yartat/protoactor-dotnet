using System.Threading.Tasks;
using Messages;

namespace Client.Proto
{
    public interface ICluster
    {
        Task<DepositResponse> MakeDeposit(string id, string type, DepositRequest request);
    }
}