using System;
using Microsoft.AspNetCore.Mvc;

namespace RestServer.Infrastructure.AspNetCore
{
    internal sealed class ResultException : ApplicationException
    {
        public IActionResult Result { get; private set; }
        public int? HttpCode { get; private set; }

        public ResultException(IActionResult result, int? httpCode = null)
        {
            Result = result;
            HttpCode = httpCode;
        }

        public static ResultException FromObject(object obj, int? httpCode = null)
        {
            return new ResultException(new ObjectResult(obj), httpCode);
        }
    }
}
