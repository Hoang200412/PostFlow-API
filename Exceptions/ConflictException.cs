using System.Net;

namespace PostFlow.Exceptions
{
    public class ConflictException : AppException
    {
        public ConflictException(string message) 
            : base(message, HttpStatusCode.Conflict)
        {
        }
    }
}
