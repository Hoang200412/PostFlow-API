using System;
using System.Net;

namespace PostFlow.Exceptions
{
    public class AppException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public AppException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError) : base(message)
        {
            StatusCode = statusCode;
        }

        public AppException(string message, Exception innerException, HttpStatusCode statusCode = HttpStatusCode.InternalServerError) : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}
