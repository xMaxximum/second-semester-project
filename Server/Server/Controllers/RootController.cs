using Microsoft.AspNetCore.Mvc;

namespace Server.Controllers
{
    [ApiController]
    [Route(Constants.RoutePrefix)]
    public class RootController : ControllerBase
    {

        [HttpGet()]
        public string HelloWorld()
        {
            return "Hello, World!";
        }
    }
}
