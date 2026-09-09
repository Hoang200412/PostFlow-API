using System;
using System.Net;
using System.Text.Json;
using PostFlow.Exceptions;

namespace PostFlow.Middleware

{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            HttpStatusCode statusCode;
            string message;

            switch (exception)
            {
                case AppException appEx:
                    statusCode = appEx.StatusCode;
                    message = appEx.Message;
                    _logger.LogWarning("AppException [{StatusCode}]: {Message}", statusCode, message);
                    break;

                case ArgumentException argEx:
                    statusCode = HttpStatusCode.BadRequest;
                    message = argEx.Message;
                    _logger.LogWarning("ArgumentException: {Message}", message);
                    break;

                case KeyNotFoundException knfEx:
                    statusCode = HttpStatusCode.NotFound;
                    message = string.IsNullOrWhiteSpace(knfEx.Message) ? "Không tìm thấy tài nguyên yêu cầu." : knfEx.Message;
                    _logger.LogWarning("KeyNotFoundException: {Message}", message);
                    break;

                case UnauthorizedAccessException unauthEx:
                    statusCode = HttpStatusCode.Unauthorized;
                    message = string.IsNullOrWhiteSpace(unauthEx.Message) ? "Bạn không có quyền truy cập." : unauthEx.Message;
                    _logger.LogWarning("UnauthorizedAccessException: {Message}", message);
                    break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    message = "Đã có lỗi hệ thống xảy ra. Vui lòng thử lại sau.";
                    _logger.LogError(exception, "Unhandled Exception: {Message}", exception.Message);
                    break;
            }

            context.Response.StatusCode = (int)statusCode;

            var responsePayload = new
            {
                statusCode = (int)statusCode,
                message = message,
                timestamp = DateTime.UtcNow
            };

            var jsonResponse = JsonSerializer.Serialize(responsePayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}
