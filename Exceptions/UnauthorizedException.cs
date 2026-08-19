using System.Net;

namespace PostFlow.Exceptions
{
    public class UnauthorizedException : AppException
    {
        public UnauthorizedException(string message = "Bạn không có quyền truy cập. Vui lòng đăng nhập lại.") 
            : base(message, HttpStatusCode.Unauthorized)
        {
        }
    }
}
