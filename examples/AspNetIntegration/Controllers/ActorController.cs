using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AspNetIntegration.Messages;

namespace AspNetIntegration.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActorController : ControllerBase
    {
        [HttpGet]
        public ActionResult<string> Get()
        {
            return "hello";
        }
        
        [HttpPost("{id}")]
        [ProducesResponseType(typeof(HelloResponse), 201)]
        public async Task<ActionResult<HelloResponse>> Post(string id,[FromBody] HelloRequest request)
        {
            var (context, pid) = Proto.Http.Extensions.Resolve(id);
            var response = await context.RequestAsync<HelloResponse>(pid, request, TimeSpan.FromSeconds(5));
            return response;
        }
    }
}