using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Client.Filters
{
    /// <summary>
    /// Default exception filter
    /// </summary>
    /// <seealso cref="ExceptionFilterAttribute" />
    public class ProcessingExceptionFilter : ExceptionFilterAttribute
    {
        private readonly ILogger<ProcessingExceptionFilter> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProcessingExceptionFilter"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public ProcessingExceptionFilter(ILogger<ProcessingExceptionFilter> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Called when [exception].
        /// </summary>
        /// <param name="context">The exception context.</param>
        public override void OnException(ExceptionContext context)
        {
            var processingException = context?.Exception;
            if (processingException == null)
                return;

            using (_logger.BeginScope(("Session", context.HttpContext?.TraceIdentifier)))
            {
                _logger.LogError(
                    message:
                    $"{processingException.GetType().Name}. Processing ended with error message: '{processingException.Message ?? string.Empty}'",
                    exception: processingException,
                    eventId: 0);
            }

            context.Result = new JsonResult(
                new 
                {
                    message = context.Exception.Message,
                });
            ((JsonResult) context.Result).StatusCode = (int) HttpStatusCode.InternalServerError;
            context.Exception = null;
            context.ExceptionHandled = true;
        }
    }
}