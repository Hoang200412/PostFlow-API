using System.Net;

namespace PostFlow.Exceptions
{
    public class ForbiddenException : AppException
    {
        public ForbiddenException(string message) 
            : base(message, HttpStatusCode.Forbidden)
        {
        }
    }
}
