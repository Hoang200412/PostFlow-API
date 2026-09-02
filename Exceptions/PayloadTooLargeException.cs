using System.Net;

namespace PostFlow.Exceptions
{
    public class PayloadTooLargeException : AppException
    {
        public PayloadTooLargeException(string message) 
            : base(message, HttpStatusCode.RequestEntityTooLarge)
        {
        }
    }
}
