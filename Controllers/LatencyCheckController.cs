// Controllers/LatencyCheckController.cs
using Microsoft.AspNetCore.Mvc;

namespace FX5u_Web_HMI_App.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LatencyCheckController : ControllerBase
    {
        /// <summary>
        /// Lightweight heartbeat for RTT measurement.
        /// </summary>
        [HttpGet("ping")]
        public IActionResult Heartbeat()
        {
            // Return immediately with a small payload.
            return Ok("OK");
        }
    }
}
