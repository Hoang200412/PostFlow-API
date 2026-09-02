using System.Net;

namespace PostFlow.Exceptions
{
    public class NotFoundException : AppException
    {
        public NotFoundException(string message = "Không tìm thấy dữ liệu yêu cầu.") 
            : base(message, HttpStatusCode.NotFound)
        {
        }
    }
}
