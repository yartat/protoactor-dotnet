using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Client.Contracts;
using Client.Filters;
using Client.Proto;
using Google.Protobuf.WellKnownTypes;
using Messages;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Client.Controllers
{
    /// <summary> 
    /// Provides API to retrieve and update currency rates.
    /// </summary>
    [Produces("application/json")]
    [Route("api/v1/payment")]
    [ServiceFilter(typeof(ProcessingExceptionFilter))]
    public class CurrencyController : Controller
    {
        private const string GeneralProduct = "GENERAL";
        private readonly ILogger _logger;
        private readonly ICluster _cluster;

        /// <summary>
        /// Initializes new instance of the <see cref="CurrencyController" /> class;
        /// </summary>
        /// <param name="cluster">The cluster.</param>
        /// <param name="logger">The logger.</param>
        public CurrencyController(
            ICluster cluster,
            ILogger<CurrencyController> logger)
        {
            _logger = logger;
            _cluster = cluster;
        }

        // POST api/v1/payment/deposit
        /// <summary>
        /// Process deposit.
        /// </summary>
        /// <param name="request">The deposit request</param>
        /// <returns>Returns result of the processing deposit.</returns>
        /// <response code="200">Returns deposit processing result.</response>
        /// <response code="500">Internal service error.</response>
        [ProducesResponseType(typeof(FinancialTransactionResponse), (int)HttpStatusCode.OK)]
        [HttpPost("deposit")]
        public async Task<ObjectResult> Deposit([FromBody] DepositTransactionRequest request)
        {
            using (_logger.BeginScope(new[]
            {
                ("Session", HttpContext.TraceIdentifier)
            }))
            {
                var result = await _cluster.MakeDeposit(
                    request.TransactionId,
                    "Player",
                    new DepositRequest
                    {
                        Id = request.TransactionId,
                        Amount = (double?) request.Amount ?? 0D,
                        Currency = request.CurrencyCode,
                        Date = request.CreatedOn != null ? request.CreatedOn.Value.ToTimestamp() : new Timestamp(),
                        Manual = request.Manual ?? false,
                        PlayerId = request.PlayerId,
                        Kiosk = request.KioskId
                    }).ConfigureAwait(false);
                return Ok(new FinancialTransactionResponse
                {
                    TransactionId = result.Id,
                    AlreadyProcessed = result.AlreadyProcessed,
                    Balances = result.Balances.ToDictionary(x => x.Key, x => (decimal)x.Value)
                });
            }
        }
    }
}
