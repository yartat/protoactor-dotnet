using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Proto;
using AspNetIntegration.Messages;

namespace AspNetIntegration.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ActorController : ControllerBase
    {
        [HttpPost("{id}")]
        public async Task<HelloResponse> Post(string id, HelloRequest request)
        {
            var context = Proto.Http.Extensions.GetRootContext();
            var pid = new PID("nohost", id);
            var response = await context.RequestAsync<HelloResponse>(pid, request, TimeSpan.FromSeconds(5));
            return response;
        }
    }
}