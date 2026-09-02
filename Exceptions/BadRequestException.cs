using System.Net;

namespace PostFlow.Exceptions
{
    public class BadRequestException : AppException
    {
        public BadRequestException(string message) 
            : base(message, HttpStatusCode.BadRequest)
        {
        }
    }
}
